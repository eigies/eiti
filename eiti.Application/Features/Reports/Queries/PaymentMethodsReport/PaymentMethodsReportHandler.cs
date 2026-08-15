using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.PaymentMethodsReport;

public sealed class PaymentMethodsReportHandler
    : IRequestHandler<PaymentMethodsReportQuery, Result<PaymentMethodsReportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly IBankRepository _bankRepository;

    public PaymentMethodsReportHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _bankRepository = bankRepository;
    }

    public async Task<Result<PaymentMethodsReportResponse>> Handle(
        PaymentMethodsReportQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PaymentMethodsReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        // El rango llega como fecha local del usuario; se traduce al instante UTC equivalente.
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        // Minorista y Mayorista se cobran por caminos distintos y viven en tablas distintas:
        //   - Minorista: venta normal -> SalePayment (lleva el medio real).
        //   - Mayorista: venta por Cuenta Corriente -> se cobra con un CustomerPayment (lleva el
        //     medio real) que se imputa FIFO como SaleCcPayment con metodo CustomerCredit.
        // Por eso filtrar las ventas por IsCuentaCorriente y despues leer SalePayments devolvia
        // vacio para Mayorista: una venta CC no tiene pagos directos.
        var saleType = (request.SaleType ?? "all").ToLowerInvariant();
        var includeRetail = saleType is "all" or "retail";
        var includeWholesale = saleType is "all" or "wholesale";

        // Agrega por medio de pago: cantidad de pagos y monto total.
        var groups = new Dictionary<int, (int Count, decimal Total)>();
        // Desglose de Tarjeta por (banco, cuotas).
        var cardGroups = new Dictionary<(int? BankId, int? Cuotas), (int Count, decimal Total)>();

        void Accumulate(SalePaymentMethod method, decimal amount, int? cardBankId, int? cardCuotas)
        {
            var key = (int)method;
            groups.TryGetValue(key, out var acc);
            groups[key] = (acc.Count + 1, acc.Total + amount);

            if (method == SalePaymentMethod.Card)
            {
                var cardKey = (cardBankId, cardCuotas);
                cardGroups.TryGetValue(cardKey, out var cardAcc);
                cardGroups[cardKey] = (cardAcc.Count + 1, cardAcc.Total + amount);
            }
        }

        if (includeRetail)
        {
            var sales = await _saleRepository.ListWithPaymentsForReportAsync(
                companyId, from, to, request.BranchId, allowedBranchIds, cancellationToken);

            // Se excluyen las ventas CC a proposito: su dinero entra por el cobro de cuenta
            // corriente. Un SalePayment sobre una venta CC es una anomalia (ver lessons.md) y
            // contarlo ademas del cobro duplicaria el importe.
            foreach (var sale in sales.Where(s => !s.IsCuentaCorriente))
            {
                foreach (var payment in sale.Payments)
                {
                    Accumulate(payment.Method, payment.Amount, payment.CardBankId, payment.CardCuotas);
                }
            }
        }

        if (includeWholesale)
        {
            var ccPayments = await _customerPaymentRepository.ListForPaymentMethodsReportAsync(
                companyId.Value, from, to, request.BranchId, allowedBranchIds, cancellationToken);

            foreach (var payment in ccPayments)
            {
                Accumulate(payment.Method, payment.Amount, payment.CardBankId, payment.CardCuotas);
            }
        }

        var grandTotal = groups.Values.Sum(v => v.Total);
        var grandCount = groups.Values.Sum(v => v.Count);

        decimal Pct(decimal value) => grandTotal == 0 ? 0 : decimal.Round(value / grandTotal * 100, 2, MidpointRounding.AwayFromZero);

        // Nombres de banco para el desglose de tarjeta.
        var bankIds = cardGroups.Keys.Where(k => k.BankId.HasValue).Select(k => k.BankId!.Value).Distinct().ToList();
        var bankNames = bankIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _bankRepository.GetByIdsAsync(bankIds, companyId, cancellationToken)).ToDictionary(b => b.Id, b => b.Name);

        var cardSubgroups = cardGroups
            .Select(kvp => new PaymentMethodsReportSubgroup(
                BuildCardLabel(kvp.Key.BankId, kvp.Key.Cuotas, bankNames),
                kvp.Key.BankId,
                kvp.Key.Cuotas,
                kvp.Value.Count,
                kvp.Value.Total,
                Pct(kvp.Value.Total)))
            .OrderByDescending(s => s.Total)
            .ToList();

        var rows = groups
            .Select(kvp => new PaymentMethodsReportRow(
                kvp.Key,
                MethodLabel((SalePaymentMethod)kvp.Key),
                kvp.Value.Count,
                kvp.Value.Total,
                Pct(kvp.Value.Total),
                kvp.Key == (int)SalePaymentMethod.Card
                    ? cardSubgroups
                    : Array.Empty<PaymentMethodsReportSubgroup>()))
            .OrderByDescending(r => r.Total)
            .ToList();

        return Result<PaymentMethodsReportResponse>.Success(
            new PaymentMethodsReportResponse(
                rows,
                new PaymentMethodsReportTotals(grandCount, grandTotal)));
    }

    private static string BuildCardLabel(int? bankId, int? cuotas, IReadOnlyDictionary<int, string> bankNames)
    {
        var bank = bankId.HasValue && bankNames.TryGetValue(bankId.Value, out var name) ? name : "Sin banco";
        var cuotasLabel = cuotas is > 0 ? $"{cuotas} cuota{(cuotas == 1 ? "" : "s")}" : "Sin cuotas";
        return $"{bank} · {cuotasLabel}";
    }

    private static string MethodLabel(SalePaymentMethod method) => method switch
    {
        SalePaymentMethod.Cash => "Efectivo",
        SalePaymentMethod.Transfer => "Transferencia",
        SalePaymentMethod.Card => "Tarjeta",
        SalePaymentMethod.Check => "Cheque",
        SalePaymentMethod.CustomerCredit => "Cuenta corriente",
        SalePaymentMethod.Other => "Otros",
        _ => "Otros"
    };
}

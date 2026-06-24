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
    private readonly IBankRepository _bankRepository;

    public PaymentMethodsReportHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
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

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        var sales = await _saleRepository.ListWithPaymentsForReportAsync(
            companyId, from, to, request.BranchId, allowedBranchIds, cancellationToken);

        // Agrega por medio de pago: cantidad de pagos y monto total.
        var groups = new Dictionary<int, (int Count, decimal Total)>();
        // Desglose de Tarjeta por (banco, cuotas).
        var cardGroups = new Dictionary<(int? BankId, int? Cuotas), (int Count, decimal Total)>();
        foreach (var sale in sales)
        {
            foreach (var payment in sale.Payments)
            {
                var key = (int)payment.Method;
                groups.TryGetValue(key, out var acc);
                groups[key] = (acc.Count + 1, acc.Total + payment.Amount);

                if (payment.Method == SalePaymentMethod.Card)
                {
                    var cardKey = (payment.CardBankId, payment.CardCuotas);
                    cardGroups.TryGetValue(cardKey, out var cardAcc);
                    cardGroups[cardKey] = (cardAcc.Count + 1, cardAcc.Total + payment.Amount);
                }
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

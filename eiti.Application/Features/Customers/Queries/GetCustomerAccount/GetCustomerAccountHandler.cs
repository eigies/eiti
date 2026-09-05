using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Customers;
using eiti.Domain.Common;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerAccount;

public sealed class GetCustomerAccountHandler
    : IRequestHandler<GetCustomerAccountQuery, Result<GetCustomerAccountResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICustomerCreditNoteRepository _creditNoteRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IChequeRepository _chequeRepository;

    public GetCustomerAccountHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICustomerCreditNoteRepository creditNoteRepository,
        ISaleRepository saleRepository,
        IChequeRepository chequeRepository)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _creditNoteRepository = creditNoteRepository;
        _saleRepository = saleRepository;
        _chequeRepository = chequeRepository;
    }

    public async Task<Result<GetCustomerAccountResponse>> Handle(
        GetCustomerAccountQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetCustomerAccountResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<GetCustomerAccountResponse>.Failure(GetCustomerAccountErrors.CustomerNotFound);

        var sales = await _saleRepository.ListCcSalesByCustomerAsync(companyId, customer.Id, cancellationToken);
        var payments = await _customerPaymentRepository.ListByCustomerAsync(companyId.Value, customer.Id.Value, cancellationToken);

        // Números de cheque para los cobros que recibieron un cheque (1 sola query, evita N+1).
        var chequeIds = payments.Where(p => p.ChequeId.HasValue).Select(p => p.ChequeId!.Value).Distinct().ToList();
        var chequeNumeroById = chequeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _chequeRepository.ListByIdsAsync(chequeIds, companyId, cancellationToken))
                .ToDictionary(c => c.Id, c => c.Numero);

        var deudaTotal = sales
            .Where(s => s.SaleStatus != SaleStatus.Cancel)
            .Sum(s => s.TotalAmount);
        var saldoPendiente = sales
            .Where(s => s.SaleStatus != SaleStatus.Cancel)
            .Sum(s => Math.Max(0m, s.CcPendingAmount));
        var cobradoTotal = payments
            .Where(p => p.Status == SaleCcPaymentStatus.Active)
            .Sum(p => p.Amount);
        cobradoTotal += sales
            .SelectMany(s => s.CcPayments)
            .Where(p => p.CustomerPaymentId is null
                && p.Method != SalePaymentMethod.CustomerCredit
                && p.Status == SaleCcPaymentStatus.Active)
            .Sum(p => p.Amount);

        // Imputaciones por cobro a nivel cliente: qué ventas cubrió cada cobro y por cuánto (solo filas activas).
        var imputacionesByPayment = new Dictionary<Guid, List<CustomerPaymentImputacion>>();
        foreach (var s in sales)
        {
            foreach (var row in s.CcPayments.Where(p =>
                p.CustomerPaymentId.HasValue && p.Status == SaleCcPaymentStatus.Active))
            {
                if (!imputacionesByPayment.TryGetValue(row.CustomerPaymentId!.Value, out var list))
                {
                    list = new List<CustomerPaymentImputacion>();
                    imputacionesByPayment[row.CustomerPaymentId.Value] = list;
                }

                list.Add(new CustomerPaymentImputacion(s.Id.Value, s.Code ?? string.Empty, row.Amount));
            }
        }

        var movements = new List<CustomerAccountMovement>();

        foreach (var s in sales)
        {
            movements.Add(new CustomerAccountMovement(
                "venta",
                s.Id.Value,
                s.CreatedAt,
                "Venta",
                s.Code,
                s.TotalAmount,
                true,
                (int)s.SaleStatus,
                s.SaleStatus.ToString(),
                null,
                null,
                null,
                null,
                null,
                null,
                s.CreatedAt));

            // Cobros legacy por-venta (SaleCcPayment con CustomerPaymentId null y método != CustomerCredit):
            // se muestran como créditos sin desglose.
            foreach (var legacy in s.CcPayments.Where(p =>
                p.CustomerPaymentId is null &&
                p.Method != SalePaymentMethod.CustomerCredit &&
                p.Status == SaleCcPaymentStatus.Active))
            {
                movements.Add(new CustomerAccountMovement(
                    "cobro",
                    legacy.Id.Value,
                    legacy.Date,
                    "Cobro (venta)",
                    s.Code,
                    legacy.Amount,
                    false,
                    (int)legacy.Status,
                    legacy.Status.ToString(),
                    legacy.Method.ToString(),
                    null,
                    null,
                    null,
                    null,
                    legacy.Notes,
                    legacy.CreatedAt));
            }
        }

        foreach (var pay in payments)
        {
            var chequeNumero = pay.ChequeId.HasValue && chequeNumeroById.TryGetValue(pay.ChequeId.Value, out var num)
                ? num
                : null;

            // Desglose cobro → ventas (solo para cobros activos; al anular las imputaciones se cancelan).
            IReadOnlyList<CustomerPaymentImputacion>? imputaciones = null;
            decimal? sobrante = null;
            if (pay.Status == SaleCcPaymentStatus.Active
                && imputacionesByPayment.TryGetValue(pay.Id, out var paymentImputaciones))
            {
                imputaciones = paymentImputaciones;
                sobrante = Math.Max(0m, pay.Amount - paymentImputaciones.Sum(i => i.Amount));
            }
            else if (pay.Status == SaleCcPaymentStatus.Active)
            {
                // Cobro activo sin imputaciones: todo quedó como saldo a favor.
                imputaciones = [];
                sobrante = pay.Amount;
            }

            movements.Add(new CustomerAccountMovement(
                "cobro",
                pay.Id,
                pay.Date,
                chequeNumero is not null ? $"Cobro · Cheque #{chequeNumero}" : "Cobro",
                null,
                pay.Amount,
                false,
                (int)pay.Status,
                pay.Status.ToString(),
                pay.Method.ToString(),
                chequeNumero,
                imputaciones,
                sobrante,
                pay.Reference,
                pay.Notes,
                pay.CreatedAt));
        }

        // Notas de crédito. Los totales no necesitan tocarse: la imputacion ya baja
        // CcPendingAmount (y con eso saldoPendiente), y cobradoTotal excluye el metodo
        // CustomerCredit, asi que la NC no cuenta como dinero cobrado. Que es lo correcto:
        // no entro un peso.
        var creditNotes = await _creditNoteRepository.ListByCustomerAsync(
            companyId.Value, customer.Id.Value, cancellationToken);

        var imputacionesByNote = new Dictionary<Guid, List<CustomerPaymentImputacion>>();
        foreach (var s in sales)
        {
            foreach (var row in s.CcPayments.Where(p =>
                p.CreditNoteId.HasValue && p.Status == SaleCcPaymentStatus.Active))
            {
                if (!imputacionesByNote.TryGetValue(row.CreditNoteId!.Value, out var list))
                {
                    list = new List<CustomerPaymentImputacion>();
                    imputacionesByNote[row.CreditNoteId.Value] = list;
                }

                list.Add(new CustomerPaymentImputacion(s.Id.Value, s.Code ?? string.Empty, row.Amount));
            }
        }

        foreach (var note in creditNotes.Where(n => n.Status == CreditNoteStatus.Active))
        {
            var imputacionesNota = imputacionesByNote.TryGetValue(note.Id, out var l)
                ? (IReadOnlyList<CustomerPaymentImputacion>)l
                : [];

            movements.Add(new CustomerAccountMovement(
                "nota_credito",
                note.Id,
                note.Date,
                note.Reason,
                note.Code,
                note.Amount,
                false,
                (int)note.Status,
                "Activa",
                null,
                null,
                imputacionesNota,
                decimal.Round(
                    note.Amount - imputacionesNota.Sum(i => i.Amount), 2, MidpointRounding.AwayFromZero),
                null,
                note.Reason,
                note.CreatedAt));
        }

        var ordered = movements
            .OrderByDescending(m => m.SortDate)
            .ThenByDescending(m => m.Date)
            .ToList();

        return Result<GetCustomerAccountResponse>.Success(new GetCustomerAccountResponse(
            customer.Id.Value,
            customer.Name,
            customer.Phone,
            customer.Email?.Value,
            deudaTotal,
            cobradoTotal,
            saldoPendiente,
            customer.CreditBalance,
            ordered));
    }
}

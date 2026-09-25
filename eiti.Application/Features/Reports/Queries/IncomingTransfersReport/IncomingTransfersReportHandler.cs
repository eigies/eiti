using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.IncomingTransfersReport;

public sealed class IncomingTransfersReportHandler
    : IRequestHandler<IncomingTransfersReportQuery, Result<IncomingTransfersReportResponse>>
{
    // Una venta reservada/pendiente puede cobrarse hasta este tiempo después de creada.
    private const int LookbackDays = 60;

    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerPaymentRepository _customerPaymentRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IBankRepository _bankRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IBranchRepository _branchRepository;

    public IncomingTransfersReportHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerPaymentRepository customerPaymentRepository,
        ICashSessionRepository cashSessionRepository,
        IBankRepository bankRepository,
        ICustomerRepository customerRepository,
        IBranchRepository branchRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerPaymentRepository = customerPaymentRepository;
        _cashSessionRepository = cashSessionRepository;
        _bankRepository = bankRepository;
        _customerRepository = customerRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Result<IncomingTransfersReportResponse>> Handle(
        IncomingTransfersReportQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IncomingTransfersReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);
        var allowedBranchIds = _currentUserService.CanViewAllBranches ? null : _currentUserService.AllowedBranchIds;

        bool BankMatches(int? bankId) => request.BankId is null || bankId == request.BankId;

        // Ventas minoristas: SalePayment con método transferencia. Las ventas CC se excluyen a
        // propósito: su dinero entra por el cobro de cuenta corriente (ver lessons.md). Se buscan
        // ventas creadas hasta LookbackDays antes del rango porque una venta reservada o pendiente
        // puede cobrarse días después; lo que decide si entra es cuándo se cobró.
        var sales = await _saleRepository.ListWithPaymentsForReportAsync(
            companyId, from.AddDays(-LookbackDays), to, request.BranchId, allowedBranchIds, cancellationToken);
        var saleTransfers = sales
            .Where(s => !s.IsCuentaCorriente)
            .SelectMany(s => s.Payments
                .Where(p => p.Method == SalePaymentMethod.Transfer)
                .Select(p => (Sale: s, Payment: p)))
            .ToList();

        // Hora exacta del cobro: el movimiento TransferIncome de esa venta con el mismo importe.
        IReadOnlyCollection<Guid>? movementBranches = request.BranchId is { } onlyBranch ? [onlyBranch] : allowedBranchIds;
        var movements = saleTransfers.Count == 0
            ? []
            : await _cashSessionRepository.ListMovementsByCompanyAsync(
                companyId, from, to, [CashMovementType.TransferIncome], movementBranches, cancellationToken);
        var movementTimes = movements
            .Where(m => m.ReferenceId.HasValue)
            .GroupBy(m => (SaleId: m.ReferenceId!.Value, m.Amount))
            .ToDictionary(g => g.Key, g => new Queue<DateTime>(g.Select(m => m.OccurredAt).Order()));
        var collectedSales = saleTransfers
            .Select(t => (t.Sale, t.Payment,
                OccurredAt: movementTimes.TryGetValue((t.Sale.Id.Value, t.Payment.Amount), out var times) && times.Count > 0
                    ? times.Dequeue()
                    : t.Sale.PaidAt ?? t.Sale.CreatedAt))
            .Where(t => t.OccurredAt >= from && t.OccurredAt <= to)
            .ToList();

        // Cobros de cuenta corriente: por el día que eligió el usuario (Date), no por cuándo se cargaron.
        var allCollections = (await _customerPaymentRepository.ListTransfersByDateAsync(
                companyId.Value, request.DateFrom.Date, request.DateTo.Date, request.BranchId, allowedBranchIds, cancellationToken))
            .Where(c => c.Method == SalePaymentMethod.Transfer)
            .ToList();

        // Sin banco receptor no se pueden conciliar contra un banco puntual: se informan aparte.
        var unassigned = request.BankId is null
            ? []
            : collectedSales.Where(t => t.Payment.TransferBankId is null).Select(t => t.Payment.Amount)
                .Concat(allCollections.Where(c => c.TransferBankId is null).Select(c => c.Amount))
                .ToList();

        var saleRows = collectedSales.Where(t => BankMatches(t.Payment.TransferBankId)).ToList();
        var collections = allCollections.Where(c => BankMatches(c.TransferBankId)).ToList();

        var bankIds = saleRows.Select(t => t.Payment.TransferBankId)
            .Concat(collections.Select(c => c.TransferBankId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var bankNames = bankIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _bankRepository.GetByIdsAsync(bankIds, companyId, cancellationToken))
                .ToDictionary(b => b.Id, b => b.Name);

        var customerIds = saleRows
            .Where(t => t.Sale.CustomerId is not null)
            .Select(t => t.Sale.CustomerId!.Value)
            .Concat(collections.Select(c => c.CustomerId))
            .Distinct()
            .ToList();
        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds.Select(id => new CustomerId(id)), cancellationToken))
                .ToDictionary(c => c.Id.Value, c => c.FullName);

        var branchNames = saleRows.Count + collections.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
                .ToDictionary(b => b.Id.Value, b => b.Name);

        static string? Lookup<TKey>(Dictionary<TKey, string> names, TKey? key) where TKey : struct =>
            key is { } k && names.TryGetValue(k, out var name) ? name : null;

        var rows = new List<IncomingTransferRow>();
        foreach (var (sale, payment, occurredAt) in saleRows)
        {
            rows.Add(new IncomingTransferRow(
                occurredAt,
                payment.Amount,
                "sale",
                payment.TransferBankId,
                Lookup(bankNames, payment.TransferBankId),
                sale.Code,
                Lookup(customerNames, sale.CustomerId?.Value),
                Lookup(branchNames, (Guid?)sale.BranchId.Value)));
        }

        foreach (var collection in collections)
        {
            // El usuario elige el día del cobro; si no trae hora, es solo una fecha.
            var dateOnly = collection.Date.TimeOfDay == TimeSpan.Zero;
            rows.Add(new IncomingTransferRow(
                collection.Date,
                collection.Amount,
                "cc_collection",
                collection.TransferBankId,
                Lookup(bankNames, collection.TransferBankId),
                collection.Reference,
                Lookup(customerNames, (Guid?)collection.CustomerId),
                Lookup(branchNames, (Guid?)collection.BranchId),
                dateOnly));
        }

        return Result<IncomingTransfersReportResponse>.Success(new IncomingTransfersReportResponse(
            rows.OrderBy(r => r.OccurredAt).ToList(),
            BranchScoped: allowedBranchIds is not null,
            UnassignedCount: unassigned.Count,
            UnassignedTotal: unassigned.Sum()));
    }
}

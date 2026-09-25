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
        // propósito: su dinero entra por el cobro de cuenta corriente (ver lessons.md).
        var sales = await _saleRepository.ListWithPaymentsForReportAsync(
            companyId, from, to, request.BranchId, allowedBranchIds, cancellationToken);
        var saleTransfers = sales
            .Where(s => !s.IsCuentaCorriente)
            .SelectMany(s => s.Payments
                .Where(p => p.Method == SalePaymentMethod.Transfer && BankMatches(p.TransferBankId))
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

        var collections = (await _customerPaymentRepository.ListForPaymentMethodsReportAsync(
                companyId.Value, from, to, request.BranchId, allowedBranchIds, cancellationToken))
            .Where(p => p.Method == SalePaymentMethod.Transfer && BankMatches(p.TransferBankId))
            .ToList();

        var bankIds = saleTransfers.Select(t => t.Payment.TransferBankId)
            .Concat(collections.Select(c => c.TransferBankId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var bankNames = bankIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _bankRepository.GetByIdsAsync(bankIds, companyId, cancellationToken))
                .ToDictionary(b => b.Id, b => b.Name);

        var customerIds = saleTransfers
            .Where(t => t.Sale.CustomerId is not null)
            .Select(t => t.Sale.CustomerId!.Value)
            .Concat(collections.Select(c => c.CustomerId))
            .Distinct()
            .ToList();
        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds.Select(id => new CustomerId(id)), cancellationToken))
                .ToDictionary(c => c.Id.Value, c => c.FullName);

        var branchNames = saleTransfers.Count + collections.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
                .ToDictionary(b => b.Id.Value, b => b.Name);

        static string? Lookup<TKey>(Dictionary<TKey, string> names, TKey? key) where TKey : struct =>
            key is { } k && names.TryGetValue(k, out var name) ? name : null;

        var rows = new List<IncomingTransferRow>();
        foreach (var (sale, payment) in saleTransfers)
        {
            var occurredAt = movementTimes.TryGetValue((sale.Id.Value, payment.Amount), out var times) && times.Count > 0
                ? times.Dequeue()
                : sale.PaidAt ?? sale.CreatedAt;
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
            // Date suele venir sin hora (el usuario elige el día): en ese caso vale el momento de carga.
            var occurredAt = collection.Date.TimeOfDay == TimeSpan.Zero ? collection.CreatedAt : collection.Date;
            rows.Add(new IncomingTransferRow(
                occurredAt,
                collection.Amount,
                "cc_collection",
                collection.TransferBankId,
                Lookup(bankNames, collection.TransferBankId),
                collection.Reference,
                Lookup(customerNames, (Guid?)collection.CustomerId),
                Lookup(branchNames, (Guid?)collection.BranchId)));
        }

        return Result<IncomingTransfersReportResponse>.Success(
            new IncomingTransfersReportResponse(rows.OrderBy(r => r.OccurredAt).ToList()));
    }
}

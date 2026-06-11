using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.ListPurchases;

public sealed class ListPurchasesHandler : IRequestHandler<ListPurchasesQuery, Result<ListPurchasesResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;

    public ListPurchasesHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<ListPurchasesResponse>> Handle(ListPurchasesQuery query, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ListPurchasesResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        PurchaseStatus? statusFilter = null;
        if (query.Status.HasValue)
        {
            if (!Enum.IsDefined(typeof(PurchaseStatus), query.Status.Value))
                return Result<ListPurchasesResponse>.Failure(
                    Error.Validation("Purchases.List.InvalidStatus", "The specified status is invalid."));

            statusFilter = (PurchaseStatus)query.Status.Value;
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await _purchaseRepository.CountAsync(
            companyId.Value, query.SupplierId, statusFilter, query.From, query.To, cancellationToken);

        var purchases = await _purchaseRepository.ListAsync(
            companyId.Value, query.SupplierId, statusFilter, query.From, query.To, page, pageSize, cancellationToken);

        // Load supplier names for all unique supplier IDs
        var supplierMap = new Dictionary<Guid, string>();
        var supplierIds = purchases.Select(p => p.SupplierId).Distinct().ToList();
        foreach (var supplierId in supplierIds)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId, companyId.Value, cancellationToken);
            if (supplier is not null)
                supplierMap[supplierId] = supplier.Name;
        }

        var items = purchases.Select(p => new ListPurchasesItemResponse(
            p.Id,
            p.SupplierId,
            supplierMap.TryGetValue(p.SupplierId, out var name) ? name : null,
            p.InvoiceNumber,
            (int)p.Status,
            p.Status.ToString(),
            p.TotalAmount,
            p.TaxAmount,
            p.GrandTotal,
            p.TotalPaid,
            p.PendingAmount,
            p.CreatedAt,
            p.Details.Count)).ToList();

        return Result<ListPurchasesResponse>.Success(
            new ListPurchasesResponse(items, totalCount, page, pageSize));
    }
}

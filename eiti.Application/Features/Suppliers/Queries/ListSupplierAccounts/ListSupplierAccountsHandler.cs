using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.ListSupplierAccounts;

public sealed class ListSupplierAccountsHandler
    : IRequestHandler<ListSupplierAccountsQuery, Result<IReadOnlyList<SupplierAccountListItem>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IPurchaseRepository _purchaseRepository;

    public ListSupplierAccountsHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        IPurchaseRepository purchaseRepository)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _purchaseRepository = purchaseRepository;
    }

    public async Task<Result<IReadOnlyList<SupplierAccountListItem>>> Handle(
        ListSupplierAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<SupplierAccountListItem>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var suppliers = await _supplierRepository.ListAsync(companyId.Value, activeOnly: true, request.Search, cancellationToken);
        var pendingBySupplier = await _purchaseRepository.GetPendingTotalsBySupplierAsync(companyId.Value, cancellationToken);

        var items = suppliers
            .Select(s => new SupplierAccountListItem(
                s.Id,
                s.Name,
                s.Phone,
                pendingBySupplier.TryGetValue(s.Id, out var pending) ? pending : 0m,
                s.CreditBalance))
            .ToList();

        return Result<IReadOnlyList<SupplierAccountListItem>>.Success(items);
    }
}

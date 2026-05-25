using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.ListSuppliers;

public sealed class ListSuppliersHandler : IRequestHandler<ListSuppliersQuery, Result<List<ListSuppliersResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;

    public ListSuppliersHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<List<ListSuppliersResponse>>> Handle(ListSuppliersQuery query, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<List<ListSuppliersResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var suppliers = await _supplierRepository.ListAsync(
            companyId.Value,
            query.ActiveOnly,
            query.Search,
            cancellationToken);

        var response = suppliers.Select(s => new ListSuppliersResponse(
            s.Id,
            s.Name,
            s.Phone,
            s.Email,
            s.TaxId,
            s.IsActive)).ToList();

        return Result<List<ListSuppliersResponse>>.Success(response);
    }
}

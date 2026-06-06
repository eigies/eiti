using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetProductReservations;

public sealed class GetProductReservationsHandler
    : IRequestHandler<GetProductReservationsQuery, Result<ProductReservationsResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;

    public GetProductReservationsHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<ProductReservationsResponse>> Handle(
        GetProductReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ProductReservationsResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        BranchId? branchFilter = null;
        if (request.BranchId.HasValue)
        {
            var access = _currentUserService.EnsureBranchAccess(request.BranchId.Value);
            if (access.IsFailure)
                return Result<ProductReservationsResponse>.Failure(access.Error);
            branchFilter = new BranchId(request.BranchId.Value);
        }

        var productId = new ProductId(request.ProductId);
        var sales = await _saleRepository.ListReservingByProductAsync(
            companyId, productId, branchFilter, cancellationToken);

        // Alcance global: limitar a las sucursales permitidas del usuario.
        if (request.BranchId is null && !_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            sales = sales.Where(s => allowed.Contains(s.BranchId.Value)).ToList();
        }

        var branchNames = new Dictionary<Guid, string>();
        async Task<string> ResolveBranchName(Guid id)
        {
            if (branchNames.TryGetValue(id, out var cached)) return cached;
            var branch = await _branchRepository.GetByIdAsync(new BranchId(id), companyId, cancellationToken);
            var name = branch?.Name ?? "(Sucursal)";
            branchNames[id] = name;
            return name;
        }

        var items = new List<ProductReservationItem>();
        foreach (var sale in sales)
        {
            var detail = sale.Details.FirstOrDefault(d => d.ProductId == productId);
            if (detail is null || detail.Quantity <= 0)
                continue;

            var customerName = "Consumidor final";
            var customerId = sale.CustomerId;
            if (customerId is not null)
            {
                var customer = await _customerRepository.GetByIdAsync(customerId, companyId, cancellationToken);
                customerName = customer?.FullName ?? "(Cliente)";
            }

            items.Add(new ProductReservationItem(
                sale.Id.Value,
                sale.Code,
                sale.BranchId.Value,
                await ResolveBranchName(sale.BranchId.Value),
                customerName,
                sale.IsCuentaCorriente,
                detail.Quantity,
                sale.IsCuentaCorriente ? sale.CcPendingAmount : sale.PendingAmount,
                sale.CreatedAt));
        }

        var total = items.Sum(i => i.Quantity);

        return Result<ProductReservationsResponse>.Success(
            new ProductReservationsResponse(request.ProductId, request.BranchId, total, items));
    }
}

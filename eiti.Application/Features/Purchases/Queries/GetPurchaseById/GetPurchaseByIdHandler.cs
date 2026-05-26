using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.GetPurchaseById;

public sealed class GetPurchaseByIdHandler : IRequestHandler<GetPurchaseByIdQuery, Result<GetPurchaseByIdResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly ISupplierRepository _supplierRepository;

    public GetPurchaseByIdHandler(
        ICurrentUserService currentUserService,
        IPurchaseRepository purchaseRepository,
        ISupplierRepository supplierRepository)
    {
        _currentUserService = currentUserService;
        _purchaseRepository = purchaseRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<GetPurchaseByIdResponse>> Handle(GetPurchaseByIdQuery query, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetPurchaseByIdResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var purchase = await _purchaseRepository.GetByIdAsync(query.Id, companyId.Value, cancellationToken);
        if (purchase is null)
            return Result<GetPurchaseByIdResponse>.Failure(GetPurchaseByIdErrors.NotFound);

        string? supplierName = null;
        string? supplierPhone = null;
        string? supplierEmail = null;

        if (purchase.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetByIdAsync(purchase.SupplierId.Value, companyId.Value, cancellationToken);
            if (supplier is not null)
            {
                supplierName = supplier.Name;
                supplierPhone = supplier.Phone;
                supplierEmail = supplier.Email;
            }
        }

        var activePayments = purchase.Payments
            .Where(p => p.Status == PurchasePaymentStatus.Active)
            .Select(p => new GetPurchasePaymentResponse(
                p.Id,
                (int)p.Method,
                p.Method.ToString(),
                p.Amount,
                (int)p.Status,
                p.Status.ToString(),
                p.Reference,
                p.Notes,
                p.Date,
                p.CreatedAt,
                p.IvaPct,
                p.IngresosBrutosPct,
                p.IvaPct.HasValue ? decimal.Round(p.Amount * p.IvaPct.Value / 100m, 2, MidpointRounding.AwayFromZero) : null,
                p.IngresosBrutosPct.HasValue ? decimal.Round(p.Amount * p.IngresosBrutosPct.Value / 100m, 2, MidpointRounding.AwayFromZero) : null))
            .ToList();

        var details = purchase.Details.Select(d => new GetPurchaseDetailResponse(
            d.Id,
            d.ProductId,
            d.ProductName,
            d.Quantity,
            d.UnitCost,
            d.TotalAmount)).ToList();

        return Result<GetPurchaseByIdResponse>.Success(new GetPurchaseByIdResponse(
            purchase.Id,
            purchase.Code,
            purchase.BranchId,
            purchase.SupplierId,
            supplierName,
            supplierPhone,
            supplierEmail,
            purchase.InvoiceNumber,
            purchase.Notes,
            (int)purchase.Status,
            purchase.Status.ToString(),
            purchase.TotalAmount,
            purchase.TotalPaid,
            purchase.PendingAmount,
            purchase.CreatedAt,
            purchase.CreatedByUserId,
            purchase.PaidAt,
            purchase.CancelledAt,
            details,
            activePayments));
    }
}

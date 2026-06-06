using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetProductReservations;

public sealed record GetProductReservationsQuery(Guid ProductId, Guid? BranchId)
    : IRequest<Result<ProductReservationsResponse>>;

public sealed record ProductReservationsResponse(
    Guid ProductId,
    Guid? BranchId,
    int TotalReserved,
    IReadOnlyList<ProductReservationItem> Items);

public sealed record ProductReservationItem(
    Guid SaleId,
    string? SaleCode,
    Guid BranchId,
    string BranchName,
    string CustomerName,
    bool IsCuentaCorriente,
    int Quantity,
    decimal PendingAmount,
    DateTime CreatedAt);

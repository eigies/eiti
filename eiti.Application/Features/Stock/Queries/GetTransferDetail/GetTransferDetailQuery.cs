using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetTransferDetail;

public sealed record GetTransferDetailQuery(Guid ReferenceId)
    : IRequest<Result<TransferDetailResponse>>;

public sealed record TransferDetailResponse(
    Guid ReferenceId,
    DateTime Date,
    Guid? FromBranchId,
    string? FromBranchName,
    Guid? ToBranchId,
    string? ToBranchName,
    string? Description,
    IReadOnlyList<TransferDetailItem> Items);

public sealed record TransferDetailItem(
    string Code,
    string Brand,
    string Name,
    int Quantity);

namespace eiti.Application.Features.CashDrawers.Common;

public sealed record CashDrawerResponse(
    Guid Id,
    Guid BranchId,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

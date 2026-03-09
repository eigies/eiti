namespace eiti.Application.Features.Branches.Common;

public sealed record BranchResponse(
    Guid Id,
    string Name,
    string? Code,
    string? Address,
    int SalesCount,
    decimal CashValue,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

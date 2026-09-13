namespace eiti.Application.Features.Branches.Common;

public sealed record BranchResponse(
    Guid Id,
    string Name,
    string? Code,
    string? Address,
    bool? AutomaticInvoicing,
    int SalesCount,
    decimal CashValue,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

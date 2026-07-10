using eiti.Application.Features.Payroll.Liquidations;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed record ListLiquidationsResponse(
    IReadOnlyList<PayrollLiquidationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

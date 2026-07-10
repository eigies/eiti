using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.ListLiquidations;

public sealed record ListLiquidationsQuery(
    Guid? EmployeeId,
    string? PeriodLabel,
    int? Status,
    int Page,
    int PageSize) : IRequest<Result<ListLiquidationsResponse>>;

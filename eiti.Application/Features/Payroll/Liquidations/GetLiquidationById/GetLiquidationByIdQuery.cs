using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public sealed record GetLiquidationByIdQuery(Guid LiquidationId) : IRequest<Result<PayrollLiquidationResponse>>;

using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Queries.ListPayrollBonuses;

public sealed record ListPayrollBonusesQuery(Guid? EmployeeId, int? Status) : IRequest<Result<IReadOnlyList<PayrollBonusResponse>>>;

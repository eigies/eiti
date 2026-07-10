using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Queries.ListPayrollAdvances;

public sealed record ListPayrollAdvancesQuery(Guid? EmployeeId, int? Status) : IRequest<Result<IReadOnlyList<PayrollAdvanceResponse>>>;

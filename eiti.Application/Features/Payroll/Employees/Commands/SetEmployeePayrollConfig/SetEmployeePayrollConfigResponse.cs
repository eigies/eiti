namespace eiti.Application.Features.Payroll.Employees.Commands.SetEmployeePayrollConfig;

public sealed record SetEmployeePayrollConfigResponse(Guid EmployeeId, decimal? BaseSalary, int? PayrollPeriodicity);

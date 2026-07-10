namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed record PayrollLiquidationSummary(Guid Id, Guid EmployeeId, string EmployeeName, decimal NetAmount);

public sealed record GeneratePayrollPeriodSkippedItem(Guid EmployeeId, string EmployeeName, string Reason);

public sealed record GeneratePayrollPeriodResponse(
    int GeneratedCount,
    IReadOnlyList<PayrollLiquidationSummary> Generated,
    IReadOnlyList<GeneratePayrollPeriodSkippedItem> Skipped);

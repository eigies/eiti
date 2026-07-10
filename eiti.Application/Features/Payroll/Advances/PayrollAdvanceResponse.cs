namespace eiti.Application.Features.Payroll.Advances;

public sealed record PayrollAdvanceResponse(
    Guid Id,
    Guid EmployeeId,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int Status,
    Guid? AppliedToLiquidationId,
    Guid? CashSessionId);

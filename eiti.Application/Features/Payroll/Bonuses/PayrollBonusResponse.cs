namespace eiti.Application.Features.Payroll.Bonuses;

public sealed record PayrollBonusResponse(
    Guid Id,
    Guid EmployeeId,
    Guid ConceptId,
    int AmountType,
    decimal Value,
    string? Notes,
    int Status,
    Guid? PayrollLiquidationId);

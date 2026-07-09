namespace eiti.Application.Features.Payroll.DeductionConcepts;

public sealed record DeductionConceptResponse(Guid Id, string Name, decimal Percentage, bool IsActive);

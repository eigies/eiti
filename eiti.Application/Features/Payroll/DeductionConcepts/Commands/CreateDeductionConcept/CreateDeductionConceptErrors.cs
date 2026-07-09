using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public static class CreateDeductionConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.DeductionConcepts.Create.Unauthorized",
        "Authentication is required.");
}

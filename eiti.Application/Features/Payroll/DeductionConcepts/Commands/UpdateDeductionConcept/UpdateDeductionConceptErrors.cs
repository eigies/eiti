using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public static class UpdateDeductionConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.DeductionConcepts.Update.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.DeductionConcepts.Update.NotFound",
        "The requested deduction concept was not found.");
}

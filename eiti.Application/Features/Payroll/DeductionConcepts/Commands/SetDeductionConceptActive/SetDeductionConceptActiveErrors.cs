using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;

public static class SetDeductionConceptActiveErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payroll.DeductionConcepts.SetActive.NotFound",
        "The requested deduction concept was not found.");
}

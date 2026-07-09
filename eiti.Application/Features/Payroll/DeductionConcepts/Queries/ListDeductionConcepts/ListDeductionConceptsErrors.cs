using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;

public static class ListDeductionConceptsErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.DeductionConcepts.List.Unauthorized",
        "Authentication is required.");
}

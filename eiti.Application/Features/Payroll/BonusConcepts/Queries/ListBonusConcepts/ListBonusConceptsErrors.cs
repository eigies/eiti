using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public static class ListBonusConceptsErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.List.Unauthorized",
        "Authentication is required.");
}

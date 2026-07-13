using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;

public static class SetBonusConceptActiveErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.SetActive.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.BonusConcepts.SetActive.NotFound",
        "The requested bonus concept was not found.");
}

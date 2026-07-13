using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public static class UpdateBonusConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.Update.Unauthorized",
        "Authentication is required.");

    public static readonly Error NotFound = Error.NotFound(
        "Payroll.BonusConcepts.Update.NotFound",
        "The requested bonus concept was not found.");
}

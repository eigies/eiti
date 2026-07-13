using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public static class CreateBonusConceptErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.BonusConcepts.Create.Unauthorized",
        "Authentication is required.");
}

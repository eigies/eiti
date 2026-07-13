using eiti.Application.Common;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public static class CreatePayrollBonusErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Payroll.Bonuses.Create.Unauthorized",
        "Authentication is required.");

    public static readonly Error EmployeeNotFound = Error.NotFound(
        "Payroll.Bonuses.Create.EmployeeNotFound",
        "The requested employee was not found.");

    public static readonly Error ConceptNotFound = Error.NotFound(
        "Payroll.Bonuses.Create.ConceptNotFound",
        "The requested bonus concept was not found.");
}

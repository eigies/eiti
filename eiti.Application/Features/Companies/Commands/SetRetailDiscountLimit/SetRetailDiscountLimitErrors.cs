using eiti.Application.Common;

namespace eiti.Application.Features.Companies.Commands.SetRetailDiscountLimit;

public static class SetRetailDiscountLimitErrors
{
    public static readonly Error CompanyNotFound = Error.NotFound(
        "Companies.SetRetailDiscountLimit.NotFound",
        "The current company was not found.");
}

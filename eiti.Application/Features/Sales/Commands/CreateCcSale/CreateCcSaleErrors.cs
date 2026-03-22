using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public static class CreateCcSaleErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.CreateCc.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error BranchNotFound = Error.NotFound(
        "Sales.CreateCc.BranchNotFound",
        "The requested branch was not found.");

    public static readonly Error CustomerRequired = Error.Validation(
        "Sales.CreateCc.CustomerRequired",
        "A customer is required for Cuenta Corriente sales.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.CreateCc.CustomerNotFound",
        "The selected customer was not found.");
}

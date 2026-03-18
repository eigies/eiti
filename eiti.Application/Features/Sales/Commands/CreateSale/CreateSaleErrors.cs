using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CreateSale;

public static class CreateSaleErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.Create.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Sales.Create.InvalidStatus",
        "The selected sale status is invalid.");

    public static readonly Error PaymentForbidden = Error.Forbidden(
        "Sales.Create.PaymentForbidden",
        "The current user does not have permission to charge sales.");

    public static readonly Error CancelNotAllowed = Error.Validation(
        "Sales.Create.CancelNotAllowed",
        "A sale cannot be created with Cancel status.");

    public static readonly Error BranchNotFound = Error.NotFound(
        "Sales.Create.BranchNotFound",
        "The requested branch was not found.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.Create.CustomerNotFound",
        "The selected customer was not found.");

    public static readonly Error CashDrawerRequired = Error.Validation(
        "Sales.Create.CashDrawerRequired",
        "A cash drawer is required when cash amount is greater than zero.");

    public static readonly Error CashSessionRequired = Error.Conflict(
        "Sales.Create.CashSessionRequired",
        "An open cash session is required for the selected cash drawer.");
}

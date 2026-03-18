using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public static class UpdateSaleErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.Update.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Sales.Update.InvalidStatus",
        "The selected sale status is invalid.");

    public static readonly Error NotFound = Error.NotFound(
        "Sales.Update.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotEditable = Error.Conflict(
        "Sales.Update.NotEditable",
        "Only sales in OnHold status can be modified.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.Update.CustomerNotFound",
        "The selected customer was not found.");

    public static readonly Error PaymentForbidden = Error.Forbidden(
        "Sales.Update.PaymentForbidden",
        "The current user does not have permission to charge sales.");

    public static readonly Error CashDrawerRequired = Error.Validation(
        "Sales.Update.CashDrawerRequired",
        "A cash drawer is required when cash amount is greater than zero.");

    public static readonly Error CashSessionRequired = Error.Conflict(
        "Sales.Update.CashSessionRequired",
        "An open cash session is required for the selected cash drawer.");
}

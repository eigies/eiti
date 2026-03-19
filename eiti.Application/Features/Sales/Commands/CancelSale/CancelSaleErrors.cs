using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public static class CancelSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.Cancel.NotFound",
        "The requested sale was not found.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Sales.Cancel.AlreadyCancelled",
        "The sale is already cancelled.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Sales.Cancel.CashSessionNotFound",
        "The cash session associated with this sale was not found.");

    public static readonly Error CashSessionClosed = Error.Conflict(
        "Sales.Cancel.CashSessionClosed",
        "The cash session associated with this sale is no longer open.");
}

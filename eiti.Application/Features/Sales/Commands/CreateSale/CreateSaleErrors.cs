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
        "A cash drawer with an open session is required to charge a sale by cash, transfer or card.");

    public static readonly Error CashSessionRequired = Error.Conflict(
        "Sales.Create.CashSessionRequired",
        "An open cash session is required for the selected cash drawer.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Sales.Create.CashSessionFromPreviousDay",
        "The open cash session belongs to a previous day. Please close it and open a new one before creating sales.");

    public static readonly Error CardBankInvalid = Error.Validation(
        "Sales.Create.CardBankInvalid",
        "El banco seleccionado no esta habilitado para tarjetas.");

    public static readonly Error TransferBankInvalid = Error.Validation(
        "Sales.Create.TransferBankInvalid",
        "El banco seleccionado no esta habilitado para transferencias.");

    public static readonly Error ChequeBankInvalid = Error.Validation(
        "Sales.Create.ChequeBankInvalid",
        "El banco seleccionado no esta habilitado como banco emisor de cheques.");
}

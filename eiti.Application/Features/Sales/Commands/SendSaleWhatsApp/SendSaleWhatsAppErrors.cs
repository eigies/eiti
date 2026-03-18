using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;

public static class SendSaleWhatsAppErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.SendWhatsApp.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error Forbidden = Error.Forbidden(
        "Sales.SendWhatsApp.Forbidden",
        "The current user does not have permission to prepare WhatsApp notifications for sales.");

    public static readonly Error NotFound = Error.NotFound(
        "Sales.SendWhatsApp.NotFound",
        "The requested sale was not found.");

    public static readonly Error NotPaid = Error.Conflict(
        "Sales.SendWhatsApp.NotPaid",
        "Only paid sales can trigger a WhatsApp notification.");

    public static readonly Error CustomerRequired = Error.Validation(
        "Sales.SendWhatsApp.CustomerRequired",
        "The sale does not have an associated customer.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.SendWhatsApp.CustomerNotFound",
        "The customer associated with this sale was not found.");

    public static readonly Error CustomerPhoneRequired = Error.Validation(
        "Sales.SendWhatsApp.CustomerPhoneRequired",
        "The customer does not have a phone number configured.");

    public static readonly Error CompanyNotFound = Error.NotFound(
        "Sales.SendWhatsApp.CompanyNotFound",
        "The current company was not found.");

    public static readonly Error Disabled = Error.Conflict(
        "Sales.SendWhatsApp.Disabled",
        "WhatsApp notifications are disabled for the current company.");

    public static readonly Error SenderPhoneRequired = Error.Validation(
        "Sales.SendWhatsApp.SenderPhoneRequired",
        "A WhatsApp sender phone is required for the current company.");

    public static readonly Error CustomerPhoneInvalid = Error.Validation(
        "Sales.SendWhatsApp.CustomerPhoneInvalid",
        "The customer phone number is invalid for WhatsApp.");
}

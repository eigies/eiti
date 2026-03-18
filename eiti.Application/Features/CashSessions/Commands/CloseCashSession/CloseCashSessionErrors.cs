using eiti.Application.Common;

namespace eiti.Application.Features.CashSessions.Commands.CloseCashSession;

public static class CloseCashSessionErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "CashSessions.Close.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error NotFound = Error.NotFound(
        "CashSessions.Close.NotFound",
        "The requested cash session was not found.");

    public static readonly Error InvalidOperation = Error.Conflict(
        "CashSessions.Close.InvalidOperation",
        "The cash session cannot be closed due to an invalid operation.");
}

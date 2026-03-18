using eiti.Application.Common;

namespace eiti.Application.Features.CashSessions.Commands.OpenCashSession;

public static class OpenCashSessionErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "CashSessions.Open.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error CashDrawerNotFound = Error.NotFound(
        "CashSessions.Open.CashDrawerNotFound",
        "The requested cash drawer was not found.");

    public static readonly Error AlreadyOpen = Error.Conflict(
        "CashSessions.Open.AlreadyOpen",
        "The cash drawer already has an open session.");
}

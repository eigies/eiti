using eiti.Application.Common;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashTransfer;

public static class CashTransferErrors
{
    public static readonly Error SourceSessionNotFound = Error.NotFound(
        "CashTransfer.SourceSessionNotFound",
        "The source cash drawer does not have an active session.");

    public static readonly Error TargetSessionNotFound = Error.NotFound(
        "CashTransfer.TargetSessionNotFound",
        "The target cash drawer does not have an active session.");

    public static readonly Error SameSession = Error.Conflict(
        "CashTransfer.SameSession",
        "Source and target cash drawers must be different.");

    public static readonly Error SourceSessionNotOpen = Error.Conflict(
        "CashTransfer.SourceSessionNotOpen",
        "The source cash session is not open.");

    public static readonly Error TargetSessionNotOpen = Error.Conflict(
        "CashTransfer.TargetSessionNotOpen",
        "The target cash session is not open.");

    public static readonly Error InvalidOperation = Error.Conflict(
        "CashTransfer.InvalidOperation",
        "The cash transfer could not be completed due to an invalid operation.");
}

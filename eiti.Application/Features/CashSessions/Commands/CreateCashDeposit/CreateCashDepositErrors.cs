using eiti.Application.Common;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashDeposit;

public static class CreateCashDepositErrors
{
    public static readonly Error SessionNotFound = Error.NotFound(
        "CashSessions.Deposit.NotFound",
        "The requested cash session was not found.");

    public static readonly Error InvalidOperation = Error.Conflict(
        "CashSessions.Deposit.InvalidOperation",
        "The cash deposit could not be registered.");
}

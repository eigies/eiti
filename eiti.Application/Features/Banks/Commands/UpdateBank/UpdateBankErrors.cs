using eiti.Application.Common;

namespace eiti.Application.Features.Banks.Commands.UpdateBank;

public static class UpdateBankErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Banks.Update.NotFound",
        "The bank was not found.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Banks.Update.Unauthorized",
        "Authentication is required.");
}

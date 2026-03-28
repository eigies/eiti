using eiti.Application.Common;

namespace eiti.Application.Features.Cheques.Commands.UpdateChequeStatus;

public static class UpdateChequeStatusErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Cheques.UpdateStatus.NotFound",
        "The cheque was not found.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Cheques.UpdateStatus.Unauthorized",
        "Authentication is required.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Cheques.UpdateStatus.InvalidStatus",
        "The provided status value is not valid.");
}

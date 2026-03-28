using eiti.Application.Common;

namespace eiti.Application.Features.Banks.Commands.CreateBank;

public static class CreateBankErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Banks.Create.Unauthorized",
        "Authentication is required.");
}

using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public static class CreateQuoteErrors
{
    public static readonly Error BranchNotFound = Error.NotFound(
        "Quotes.Create.BranchNotFound",
        "The requested branch was not found.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Quotes.Create.CustomerNotFound",
        "The selected customer was not found.");

    public static readonly Error ProductNotFound = Error.NotFound(
        "Quotes.Create.ProductNotFound",
        "One of the requested products was not found.");

    public static readonly Error InvalidCustomerOrProspect = Error.Validation(
        "Quotes.Create.InvalidCustomerOrProspect",
        "A quote must have exactly one of an existing customer or a prospect name.");

    public static Error InvalidInput(string message) =>
        Error.Validation("Quotes.Create.InvalidInput", message);
}

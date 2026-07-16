using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.CancelQuote;

public static class CancelQuoteErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.Cancel.QuoteNotFound",
        "The requested quote was not found.");
}

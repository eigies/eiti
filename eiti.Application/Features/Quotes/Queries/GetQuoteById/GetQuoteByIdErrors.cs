using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public static class GetQuoteByIdErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.GetById.QuoteNotFound",
        "The requested quote was not found.");
}

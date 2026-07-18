using eiti.Application.Common;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public static class ConvertQuoteToSaleErrors
{
    public static readonly Error QuoteNotFound = Error.NotFound(
        "Quotes.Convert.QuoteNotFound",
        "The requested quote was not found.");

    public static readonly Error NotPending = Error.Conflict(
        "Quotes.Convert.NotPending",
        "Only a pending quote can be converted into a sale.");

    public static readonly Error Expired = Error.Conflict(
        "Quotes.Convert.Expired",
        "This quote has expired and can no longer be converted directly.");
}

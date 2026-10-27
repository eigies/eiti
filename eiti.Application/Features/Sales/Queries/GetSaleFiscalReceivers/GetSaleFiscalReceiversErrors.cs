using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Queries.GetSaleFiscalReceivers;

public static class GetSaleFiscalReceiversErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.FiscalReceivers.NotFound",
        "The requested sale was not found.");
}

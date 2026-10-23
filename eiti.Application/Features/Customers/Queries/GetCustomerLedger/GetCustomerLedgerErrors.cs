using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Queries.GetCustomerLedger;

public static class GetCustomerLedgerErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.GetLedger.CustomerNotFound",
        "El cliente no existe.");
}

using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Queries.GetCustomerAccount;

public static class GetCustomerAccountErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.GetAccount.CustomerNotFound",
        "El cliente no existe.");
}

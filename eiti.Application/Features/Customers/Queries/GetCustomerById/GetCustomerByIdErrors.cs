using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Queries.GetCustomerById;

public static class GetCustomerByIdErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Customer.GetById.NotFound",
        "El cliente no fue encontrado.");
}

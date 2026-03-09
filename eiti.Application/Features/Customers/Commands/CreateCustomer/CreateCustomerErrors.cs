using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.CreateCustomer;

public static class CreateCustomerErrors
{
    public static readonly Error EmailAlreadyExists = Error.Conflict(
        "Customer.Create.EmailAlreadyExists",
        "Ya existe un cliente con ese email.");
}

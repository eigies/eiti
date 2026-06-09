using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.DeleteCustomer;

public static class DeleteCustomerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Customers.Delete.NotFound",
        "El cliente no fue encontrado.");

    public static readonly Error InUse = Error.Conflict(
        "Customers.Delete.InUse",
        "No se puede eliminar el cliente porque tiene ventas registradas.");

    public static readonly Error HasBalance = Error.Conflict(
        "Customers.Delete.HasBalance",
        "No se puede eliminar el cliente porque tiene saldo de cuenta corriente.");
}

using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.ApplyCustomerCredit;

public static class ApplyCustomerCreditErrors
{
    public static readonly Error CustomerNotFound =
        Error.NotFound("Customers.ApplyCredit.CustomerNotFound", "No se encontró el cliente.");

    public static readonly Error NoCredit =
        Error.Validation("Customers.ApplyCredit.NoCredit", "El cliente no tiene saldo a favor para aplicar.");
}

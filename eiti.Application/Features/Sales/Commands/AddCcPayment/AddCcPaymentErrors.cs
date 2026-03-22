using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.AddCcPayment;

public static class AddCcPaymentErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.AddCcPayment.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error SaleNotFound = Error.NotFound(
        "Sales.AddCcPayment.SaleNotFound",
        "The sale was not found.");

    public static readonly Error NotCuentaCorriente = Error.Validation(
        "Sales.AddCcPayment.NotCuentaCorriente",
        "CC payments can only be added to Cuenta Corriente sales.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Sales.AddCcPayment.InvalidPaymentMethod",
        "The selected payment method is invalid.");
}

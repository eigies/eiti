using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CancelCcPayment;

public static class CancelCcPaymentErrors
{
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Sales.CancelCcPayment.Unauthorized",
        "The current user is not authenticated.");

    public static readonly Error SaleNotFound = Error.NotFound(
        "Sales.CancelCcPayment.SaleNotFound",
        "The sale was not found.");

    public static readonly Error NotCuentaCorriente = Error.Validation(
        "Sales.CancelCcPayment.NotCuentaCorriente",
        "CC payments can only be cancelled on Cuenta Corriente sales.");

    public static readonly Error PaymentNotFound = Error.NotFound(
        "Sales.CancelCcPayment.PaymentNotFound",
        "The payment was not found.");
}

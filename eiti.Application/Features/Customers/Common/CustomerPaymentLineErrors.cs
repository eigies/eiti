using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Common;

// Errores de un medio de cobro al cliente. Los comparten el alta de cobro suelto y el recibo (los dos
// registran medios con la misma regla), por eso conservan el código "AddPayment".
public static class CustomerPaymentLineErrors
{
    public static readonly Error NoLines = Error.Validation(
        "Customers.AddPayment.NoLines",
        "El cobro tiene que tener al menos un medio.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Customers.AddPayment.InvalidPaymentMethod",
        "El método de cobro es inválido.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Customers.AddPayment.InvalidAmount",
        "El monto debe ser mayor a cero.");

    public static readonly Error ChequeRequired = Error.Validation(
        "Customers.AddPayment.ChequeRequired",
        "Para cobrar con cheque debe completar los datos del cheque.");

    public static readonly Error CardBankInvalid = Error.Validation(
        "Customers.AddPayment.CardBankInvalid",
        "El banco seleccionado no esta habilitado para tarjetas.");

    public static readonly Error ChequeBankInvalid = Error.Validation(
        "Customers.AddPayment.ChequeBankInvalid",
        "El banco seleccionado no esta habilitado como banco emisor de cheques.");
}

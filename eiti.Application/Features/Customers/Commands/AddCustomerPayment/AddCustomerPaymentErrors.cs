using eiti.Application.Common;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

public static class AddCustomerPaymentErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.AddPayment.CustomerNotFound",
        "El cliente no existe.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Customers.AddPayment.InvalidPaymentMethod",
        "El método de cobro es inválido.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Customers.AddPayment.InvalidAmount",
        "El monto debe ser mayor a cero.");

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.AddPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.AddPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar cobros.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Customers.AddPayment.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar cobros.");

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

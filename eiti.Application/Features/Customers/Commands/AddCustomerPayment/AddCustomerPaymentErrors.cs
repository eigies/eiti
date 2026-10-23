using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

public static class AddCustomerPaymentErrors
{
    public static readonly Error CustomerNotFound = Error.NotFound(
        "Customers.AddPayment.CustomerNotFound",
        "El cliente no existe.");

    public static readonly Error InvalidPaymentMethod = CustomerPaymentLineErrors.InvalidPaymentMethod;

    public static readonly Error InvalidAmount = CustomerPaymentLineErrors.InvalidAmount;

    public static readonly Error NoCashSessionOpen = Error.Conflict(
        "Customers.AddPayment.NoCashSessionOpen",
        "No hay una sesión de caja abierta para tu caja asignada.");

    public static readonly Error NoAssignedCashDrawer = Error.Conflict(
        "Customers.AddPayment.NoAssignedCashDrawer",
        "No tenés una caja asignada. Pedile a un administrador que te asigne una caja para poder registrar cobros.");

    public static readonly Error CashSessionFromPreviousDay = Error.Conflict(
        "Customers.AddPayment.CashSessionFromPreviousDay",
        "La sesión de caja está abierta desde un día anterior. Cerrá la sesión antes de registrar cobros.");

    public static readonly Error ChequeRequired = CustomerPaymentLineErrors.ChequeRequired;

    public static readonly Error CardBankInvalid = CustomerPaymentLineErrors.CardBankInvalid;

    public static readonly Error TransferBankInvalid = CustomerPaymentLineErrors.TransferBankInvalid;

    public static readonly Error ChequeBankInvalid = CustomerPaymentLineErrors.ChequeBankInvalid;
}

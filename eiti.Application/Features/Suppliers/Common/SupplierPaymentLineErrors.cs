using eiti.Application.Common;

namespace eiti.Application.Features.Suppliers.Common;

// Errores de un medio de pago al proveedor. Los comparten el alta de pago suelto y la orden de pago
// (los dos registran medios con la misma regla), por eso conservan el código "AddPayment".
public static class SupplierPaymentLineErrors
{
    public static readonly Error NoLines = Error.Validation(
        "Suppliers.AddPayment.NoLines",
        "El pago tiene que tener al menos un medio.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Suppliers.AddPayment.InvalidPaymentMethod",
        "El método de pago es inválido.");

    public static readonly Error InvalidAmount = Error.Validation(
        "Suppliers.AddPayment.InvalidAmount",
        "El monto debe ser mayor a cero.");

    public static readonly Error ChequeRequired = Error.Validation(
        "Suppliers.AddPayment.ChequeRequired",
        "Para pagar con cheque debe seleccionar un cheque en cartera.");

    public static readonly Error ChequeNotFound = Error.NotFound(
        "Suppliers.AddPayment.ChequeNotFound",
        "El cheque seleccionado no existe.");

    public static readonly Error ChequeNotEnCartera = Error.Conflict(
        "Suppliers.AddPayment.ChequeNotEnCartera",
        "El cheque seleccionado no está en cartera y no puede usarse para pagar.");

    public static readonly Error DuplicateCheque = Error.Validation(
        "Suppliers.AddPayment.DuplicateCheque",
        "El mismo cheque está cargado dos veces en el pago.");
}

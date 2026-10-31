using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Common;

/// <summary>Reglas de los pagos de una venta minorista que comparten el alta y la edición.</summary>
public static class SalePaymentErrors
{
    public static readonly Error SingleCard = Error.Validation(
        "Sales.Payments.SingleCard",
        "Solo se admite un pago con tarjeta por venta.");
}

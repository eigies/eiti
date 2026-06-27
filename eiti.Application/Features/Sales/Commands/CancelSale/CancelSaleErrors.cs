using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public static class CancelSaleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Sales.Cancel.NotFound",
        "The requested sale was not found.");

    public static readonly Error AlreadyCancelled = Error.Conflict(
        "Sales.Cancel.AlreadyCancelled",
        "The sale is already cancelled.");

    public static readonly Error CashSessionNotFound = Error.NotFound(
        "Sales.Cancel.CashSessionNotFound",
        "The cash session associated with this sale was not found.");

    public static readonly Error CashSessionClosed = Error.Conflict(
        "Sales.Cancel.CashSessionClosed",
        "The cash session associated with this sale is no longer open.");

    public static readonly Error NoOpenSessionForHistoricalCancel = Error.Conflict(
        "Sales.Cancel.NoOpenSessionForHistoricalCancel",
        "No hay una sesión de caja abierta para registrar la cancelación histórica.");

    public static readonly Error RefundModeRequired = Error.Validation(
        "Sales.Cancel.RefundModeRequired",
        "La venta tiene cobros: indicá si vuelven como saldo a favor o si se anulan los pagos.");

    public static readonly Error CustomerNotFound = Error.NotFound(
        "Sales.Cancel.CustomerNotFound",
        "No se encontró el cliente de la venta para acreditar el saldo a favor.");

    public static readonly Error NoOpenSessionForRefund = Error.Conflict(
        "Sales.Cancel.NoOpenSessionForRefund",
        "Para anular los pagos en efectivo necesitás una caja abierta. Abrí caja o elegí 'saldo a favor'.");
}

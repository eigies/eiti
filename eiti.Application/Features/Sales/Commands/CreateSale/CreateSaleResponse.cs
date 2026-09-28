using eiti.Application.Features.Sales.Common;
namespace eiti.Application.Features.Sales.Commands.CreateSale;

public sealed record CreateSaleResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? CustomerDocument,
    string? CustomerTaxId,
    string? CustomerAddress,
    string? DeliveryAddress,
    Guid? CashDrawerId,
    Guid? CashSessionId,
    bool HasDelivery,
    Guid? TransportAssignmentId,
    int IdSaleStatus,
    string SaleStatus,
    decimal NoDeliverySurchargeTotal,
    decimal TotalAmount,
    decimal MonetaryPaidAmount,
    decimal TradeInAmount,
    decimal SettledAmount,
    decimal PendingAmount,
    decimal ChangeAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    bool IsModified,
    IReadOnlyList<CreateSaleDetailItemResponse> Details,
    IReadOnlyList<CreateSalePaymentItemResponse> Payments,
    IReadOnlyList<CreateSaleTradeInItemResponse> TradeIns,
    // Null cuando la venta no se facturó (no se pidió y la sucursal no factura sola).
    CreateSaleInvoicingResponse? Invoicing = null);

/// <summary>
/// Cómo terminó el pedido de factura que se hizo al crear la venta, para avisarle al vendedor en el
/// momento: autorizada, en trámite (se completa sola) o rechazada con el motivo.
/// </summary>
public sealed record CreateSaleInvoicingResponse(
    int Status,
    string StatusName,
    string? DocumentType,
    int? PointOfSale,
    long? Number,
    string? Message)
{
    public static CreateSaleInvoicingResponse? From(SaleInvoicingOutcome? outcome) =>
        outcome is null
            ? null
            : new CreateSaleInvoicingResponse(
                (int)outcome.Status,
                outcome.Status.ToString(),
                outcome.Document?.DocumentType,
                outcome.Document?.PointOfSale,
                outcome.Document?.Number,
                outcome.Message);
}

public sealed record CreateSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TotalAmount);

public sealed record CreateSalePaymentItemResponse(
    int IdPaymentMethod,
    string PaymentMethod,
    decimal Amount,
    string? Reference);

public sealed record CreateSaleTradeInItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);

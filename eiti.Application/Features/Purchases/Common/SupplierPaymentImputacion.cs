namespace eiti.Application.Features.Purchases.Common;

// Detalle de a qué compra (factura) se imputó parte de un pago de proveedor y por cuánto.
// Permite rastrear, para un pago, qué facturas cubrió y cuánto de cada una.
public sealed record SupplierPaymentImputacion(
    Guid PurchaseId,
    string Code,                // COMP-XXXX
    string? InvoiceNumber,      // Nº de factura del proveedor (si tiene)
    decimal Amount);

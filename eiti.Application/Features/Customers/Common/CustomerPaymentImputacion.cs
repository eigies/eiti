namespace eiti.Application.Features.Customers.Common;

// Detalle de a qué venta CC se imputó parte de un cobro de cliente y por cuánto.
// Permite rastrear, para un cobro, qué ventas cubrió y cuánto de cada una.
public sealed record CustomerPaymentImputacion(
    Guid SaleId,
    string Code,        // VENT-XXXX (Sale.Code)
    decimal Amount);

using eiti.Domain.Products;
using eiti.Domain.Stock;

namespace eiti.Application.Common;

/// <summary>
/// Resuelve el precio de venta y el costo efectivos de un producto en una sucursal:
/// usa el override de la fila <see cref="BranchProductStock"/> si existe, o el valor global del producto.
/// Fuente única de la regla de fallback (la consumen los handlers de venta y las vistas de stock).
/// </summary>
public static class BranchPricing
{
    public static decimal ResolvePrice(BranchProductStock stock, Product product)
        => stock.SalePriceOverride ?? product.Price;

    public static decimal ResolveCost(BranchProductStock stock, Product product)
        => stock.CostOverride ?? product.CostPrice;
}

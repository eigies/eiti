using System.Globalization;
using eiti.Application.Common;

namespace eiti.Application.Features.Sales.Common;

public static class RetailDiscountErrors
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    // El mensaje lleva el tope vigente: no puede ser static readonly.
    public static Error ExceedsLimit(decimal maxPercent) => Error.Validation(
        "Sales.Discount.ExceedsLimit",
        $"El descuento supera el máximo de {maxPercent.ToString("0.##", Argentina)}% que fijó el dueño.");
}

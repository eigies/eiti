namespace eiti.Application.Features.Sales.Common;

/// <summary>
/// Letra que el vendedor eligió en la venta. No se le manda al servicio de facturación: la letra la
/// decide la condición de IVA del comprador. Sirve para frenar la venta cuando lo que el vendedor
/// quiere emitir no coincide con los datos del cliente.
/// </summary>
public enum InvoiceLetter
{
    A = 1,
    B = 2
}

namespace eiti.Domain.Customers;

// Condición del cliente frente al IVA. Define, del lado fiscal, si corresponde Factura A o B.
// No la usa EITI para lógica interna; es un dato que se envía al servicio de fiscalización.
public enum IvaCondition
{
    ResponsableInscripto = 1,
    Monotributo = 2,
    ConsumidorFinal = 3,
    Exento = 4
}

namespace eiti.Domain.Cheques;

public enum ChequeOrigen
{
    // Lo entregó un cliente: entra a cartera y se deposita o se endosa a un proveedor.
    Tercero = 1,
    // Lo emite la empresa para pagarle a un proveedor: nunca pasa por cartera.
    Propio = 2
}

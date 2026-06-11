namespace eiti.Application.Features.Purchases.Queries.ListCarteraCheques;

public sealed record CarteraChequeResponse(
    Guid Id,
    string Numero,
    string Titular,
    string CuitDni,
    decimal Monto,
    int BankId,
    string BankName,
    DateTime FechaVencimiento);

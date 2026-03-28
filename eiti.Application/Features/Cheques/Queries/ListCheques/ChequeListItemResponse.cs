namespace eiti.Application.Features.Cheques.Queries.ListCheques;

public sealed record ChequeListItemResponse(
    Guid Id,
    string Numero,
    string BankName,
    string Titular,
    decimal Monto,
    DateTime FechaVencimiento,
    int Estado,
    string EstadoName,
    string? SaleCode,
    string SaleType);

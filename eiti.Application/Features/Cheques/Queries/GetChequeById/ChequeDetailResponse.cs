namespace eiti.Application.Features.Cheques.Queries.GetChequeById;

public sealed record ChequeDetailResponse(
    Guid Id,
    string Numero,
    int BankId,
    string BankName,
    string Titular,
    string CuitDni,
    decimal Monto,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    int Estado,
    string EstadoName,
    string? Notas,
    string? SaleCode,
    string SaleType,
    DateTime CreatedAt,
    DateTime UpdatedAt);

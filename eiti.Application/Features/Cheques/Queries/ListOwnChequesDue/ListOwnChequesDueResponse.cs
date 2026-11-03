namespace eiti.Application.Features.Cheques.Queries.ListOwnChequesDue;

public sealed record ListOwnChequesDueResponse(
    Guid Id,
    string Numero,
    string BankName,
    string? SupplierName,
    decimal Monto,
    DateTime FechaVencimiento);

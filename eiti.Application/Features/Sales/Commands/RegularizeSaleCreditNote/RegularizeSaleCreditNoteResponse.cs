namespace eiti.Application.Features.Sales.Commands.RegularizeSaleCreditNote;

/// <param name="Completed">La NC al receptor de la factura quedó autorizada.</param>
/// <param name="DebitNote">Null si en esta pasada no hizo falta (ya estaba autorizada).</param>
/// <param name="CreditNote">Null si todavía no se pudo pedir (la ND no quedó autorizada).</param>
public sealed record RegularizeSaleCreditNoteResponse(
    Guid SaleId,
    bool Completed,
    RegularizedDocumentResponse? DebitNote,
    RegularizedDocumentResponse? CreditNote,
    string? Message);

public sealed record RegularizedDocumentResponse(
    int Status,
    string StatusName,
    int? PointOfSale,
    long? Number);

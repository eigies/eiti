namespace eiti.Application.Features.Sales.Commands.RetrySaleCreditNote;

public sealed record RetrySaleCreditNoteResponse(
    Guid SaleId,
    int CreditNoteStatus,
    string CreditNoteStatusName,
    int? PointOfSale,
    long? Number,
    string? Message);

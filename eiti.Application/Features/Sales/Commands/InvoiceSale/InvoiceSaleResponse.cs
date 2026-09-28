namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

public sealed record InvoiceSaleResponse(
    Guid SaleId,
    int InvoicingStatus,
    string InvoicingStatusName,
    Guid? FiscalDocumentId,
    string? FiscalDocumentType,
    int? PointOfSale,
    long? Number,
    string? Cae,
    DateTime? CaeExpiry,
    string? QrUrl,
    string? Message);

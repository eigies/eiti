namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed record UpdateCurrentCompanyResponse(
    Guid Id,
    string Name,
    string PrimaryDomain,
    bool IsWhatsAppEnabled,
    string? WhatsAppSenderPhone,
    decimal? DefaultNoDeliverySurcharge,
    string? PdfLogoUrl,
    string? PdfWatermarkUrl,
    DateTime CreatedAt);

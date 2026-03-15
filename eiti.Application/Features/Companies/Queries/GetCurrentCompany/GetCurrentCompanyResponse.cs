namespace eiti.Application.Features.Companies.Queries.GetCurrentCompany;

public sealed record GetCurrentCompanyResponse(
    Guid Id,
    string Name,
    string PrimaryDomain,
    bool IsWhatsAppEnabled,
    string? WhatsAppSenderPhone,
    decimal? DefaultNoDeliverySurcharge,
    DateTime CreatedAt);

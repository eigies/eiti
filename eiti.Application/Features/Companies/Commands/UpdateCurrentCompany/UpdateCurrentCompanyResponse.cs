namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed record UpdateCurrentCompanyResponse(
    Guid Id,
    string Name,
    string PrimaryDomain,
    bool IsWhatsAppEnabled,
    string? WhatsAppSenderPhone,
    DateTime CreatedAt);

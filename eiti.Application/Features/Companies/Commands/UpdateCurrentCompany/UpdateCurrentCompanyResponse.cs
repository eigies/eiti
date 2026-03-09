namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed record UpdateCurrentCompanyResponse(
    Guid Id,
    string Name,
    string PrimaryDomain,
    DateTime CreatedAt);

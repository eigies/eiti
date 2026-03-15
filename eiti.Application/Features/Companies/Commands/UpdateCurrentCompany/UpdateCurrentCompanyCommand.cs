using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed record UpdateCurrentCompanyCommand(
    string Name,
    string PrimaryDomain,
    bool? IsWhatsAppEnabled,
    string? WhatsAppSenderPhone,
    decimal? DefaultNoDeliverySurcharge = null
) : IRequest<Result<UpdateCurrentCompanyResponse>>;

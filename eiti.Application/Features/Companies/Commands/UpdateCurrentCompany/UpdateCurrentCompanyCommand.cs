using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed record UpdateCurrentCompanyCommand(
    string Name,
    string PrimaryDomain
) : IRequest<Result<UpdateCurrentCompanyResponse>>;

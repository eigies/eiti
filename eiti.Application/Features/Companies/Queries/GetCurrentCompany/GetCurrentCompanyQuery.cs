using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Companies.Queries.GetCurrentCompany;

public sealed record GetCurrentCompanyQuery()
    : IRequest<Result<GetCurrentCompanyResponse>>;

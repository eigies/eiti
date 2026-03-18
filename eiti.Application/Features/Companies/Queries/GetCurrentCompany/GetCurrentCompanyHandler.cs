using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Companies.Queries.GetCurrentCompany;

public sealed class GetCurrentCompanyHandler
    : IRequestHandler<GetCurrentCompanyQuery, Result<GetCurrentCompanyResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyRepository _companyRepository;

    public GetCurrentCompanyHandler(
        ICurrentUserService currentUserService,
        ICompanyRepository companyRepository)
    {
        _currentUserService = currentUserService;
        _companyRepository = companyRepository;
    }

    public async Task<Result<GetCurrentCompanyResponse>> Handle(
        GetCurrentCompanyQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetCurrentCompanyResponse>.Failure(authCheck.Error);

        var company = await _companyRepository.GetByIdAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        if (company is null)
        {
            return Result<GetCurrentCompanyResponse>.Failure(
                Error.NotFound(
                    "Companies.GetCurrent.NotFound",
                    "The current company was not found."));
        }

        return Result<GetCurrentCompanyResponse>.Success(
            new GetCurrentCompanyResponse(
                company.Id.Value,
                company.Name.Value,
                company.PrimaryDomain.Value,
                company.IsWhatsAppEnabled,
                company.WhatsAppSenderPhone,
                company.DefaultNoDeliverySurcharge,
                company.CreatedAt));
    }
}

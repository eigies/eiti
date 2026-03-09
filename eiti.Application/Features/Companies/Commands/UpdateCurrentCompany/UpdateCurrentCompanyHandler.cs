using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Companies;
using MediatR;

namespace eiti.Application.Features.Companies.Commands.UpdateCurrentCompany;

public sealed class UpdateCurrentCompanyHandler
    : IRequestHandler<UpdateCurrentCompanyCommand, Result<UpdateCurrentCompanyResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCurrentCompanyHandler(
        ICurrentUserService currentUserService,
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateCurrentCompanyResponse>> Handle(
        UpdateCurrentCompanyCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UpdateCurrentCompanyResponse>.Failure(
                Error.Unauthorized(
                    "Companies.UpdateCurrent.Unauthorized",
                    "The current user is not authenticated."));
        }

        var company = await _companyRepository.GetByIdAsync(
            _currentUserService.CompanyId,
            cancellationToken);

        if (company is null)
        {
            return Result<UpdateCurrentCompanyResponse>.Failure(
                Error.NotFound(
                    "Companies.UpdateCurrent.NotFound",
                    "The current company was not found."));
        }

        CompanyName companyName;
        CompanyDomain companyDomain;

        try
        {
            companyName = CompanyName.Create(request.Name);
            companyDomain = CompanyDomain.Create(request.PrimaryDomain);
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateCurrentCompanyResponse>.Failure(
                Error.Validation("Companies.UpdateCurrent.InvalidInput", ex.Message));
        }

        var existingCompany = await _companyRepository.GetByPrimaryDomainAsync(
            companyDomain,
            cancellationToken);

        if (existingCompany is not null && existingCompany.Id != company.Id)
        {
            return Result<UpdateCurrentCompanyResponse>.Failure(
                Error.Conflict(
                    "Companies.UpdateCurrent.DomainAlreadyExists",
                    "Another company already uses that domain."));
        }

        company.Update(companyName, companyDomain);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UpdateCurrentCompanyResponse>.Success(
            new UpdateCurrentCompanyResponse(
                company.Id.Value,
                company.Name.Value,
                company.PrimaryDomain.Value,
                company.CreatedAt));
    }
}

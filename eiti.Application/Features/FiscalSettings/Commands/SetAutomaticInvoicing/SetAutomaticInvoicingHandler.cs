using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.SetAutomaticInvoicing;

public sealed class SetAutomaticInvoicingHandler : IRequestHandler<SetAutomaticInvoicingCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetAutomaticInvoicingHandler(
        ICurrentUserService currentUserService,
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetAutomaticInvoicingCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var company = await _companyRepository.GetByIdAsync(_currentUserService.CompanyId!, cancellationToken);
        if (company is null)
            return Result.Failure(SetAutomaticInvoicingErrors.CompanyNotFound);

        company.SetAutomaticInvoicing(request.Enabled);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

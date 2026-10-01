using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.FiscalSettings.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Queries.GetFiscalSettings;

public sealed class GetFiscalSettingsHandler : IRequestHandler<GetFiscalSettingsQuery, Result<FiscalSettingsResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFiscalPointOfSaleRepository _pointsOfSale;
    private readonly IBranchRepository _branchRepository;
    private readonly IFiscalizationService _fiscalizationService;
    private readonly ICompanyRepository _companyRepository;

    public GetFiscalSettingsHandler(
        ICurrentUserService currentUserService,
        IFiscalPointOfSaleRepository pointsOfSale,
        IBranchRepository branchRepository,
        IFiscalizationService fiscalizationService,
        ICompanyRepository companyRepository)
    {
        _currentUserService = currentUserService;
        _pointsOfSale = pointsOfSale;
        _branchRepository = branchRepository;
        _fiscalizationService = fiscalizationService;
        _companyRepository = companyRepository;
    }

    public async Task<Result<FiscalSettingsResponse>> Handle(GetFiscalSettingsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<FiscalSettingsResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        var automaticInvoicing = company?.AutomaticInvoicing ?? false;

        var pointsOfSale = await _pointsOfSale.ListByCompanyAsync(companyId, cancellationToken);
        var branches = await _branchRepository.ListByCompanyAsync(companyId, cancellationToken);
        var branchByPointOfSale = branches
            .Where(branch => branch.FiscalPointOfSaleId is not null)
            .ToDictionary(branch => branch.FiscalPointOfSaleId!);
        var rows = pointsOfSale
            .Select(pointOfSale => FiscalPointOfSaleResponse.From(pointOfSale, branchByPointOfSale.GetValueOrDefault(pointOfSale.Id)))
            .ToList();

        if (!_fiscalizationService.IsEnabled)
        {
            return Result<FiscalSettingsResponse>.Success(new(false, automaticInvoicing, null, null, rows));
        }

        var issuer = await _fiscalizationService.GetIssuerAsync(companyId.Value, cancellationToken);
        return Result<FiscalSettingsResponse>.Success(issuer.IsSuccess
            ? new(true, automaticInvoicing, FiscalIssuerResponse.From(issuer.Issuer!), null, rows)
            : new(true, automaticInvoicing, null, issuer.ErrorMessage, rows));
    }
}

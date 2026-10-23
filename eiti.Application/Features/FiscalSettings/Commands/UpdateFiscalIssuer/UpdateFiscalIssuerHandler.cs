using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.FiscalSettings.Common;
using MediatR;

namespace eiti.Application.Features.FiscalSettings.Commands.UpdateFiscalIssuer;

public sealed class UpdateFiscalIssuerHandler : IRequestHandler<UpdateFiscalIssuerCommand, Result<FiscalIssuerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IFiscalizationService _fiscalizationService;

    public UpdateFiscalIssuerHandler(ICurrentUserService currentUserService, IFiscalizationService fiscalizationService)
    {
        _currentUserService = currentUserService;
        _fiscalizationService = fiscalizationService;
    }

    public async Task<Result<FiscalIssuerResponse>> Handle(UpdateFiscalIssuerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<FiscalIssuerResponse>.Failure(authCheck.Error);

        if (!_fiscalizationService.IsEnabled)
            return Result<FiscalIssuerResponse>.Failure(UpdateFiscalIssuerErrors.NotConfigured);

        // Los datos viven en el servicio (son los que se imprimen); EITI no guarda copia.
        var result = await _fiscalizationService.UpdateIssuerAsync(
            _currentUserService.CompanyId!.Value,
            new FiscalIssuerUpdate(request.LegalName.Trim(), request.IvaCondition, request.Iibb, request.ActivityStartDate, request.CommercialAddress),
            cancellationToken);

        return result.IsSuccess
            ? Result<FiscalIssuerResponse>.Success(FiscalIssuerResponse.From(result.Issuer!))
            : Result<FiscalIssuerResponse>.Failure(UpdateFiscalIssuerErrors.NotSaved(result.ErrorMessage));
    }
}

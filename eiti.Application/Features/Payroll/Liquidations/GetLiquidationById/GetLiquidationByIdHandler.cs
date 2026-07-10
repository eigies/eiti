using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GetLiquidationById;

public sealed class GetLiquidationByIdHandler : IRequestHandler<GetLiquidationByIdQuery, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _repository;

    public GetLiquidationByIdHandler(ICurrentUserService currentUserService, IPayrollLiquidationRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(GetLiquidationByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<PayrollLiquidationResponse>.Failure(GetLiquidationByIdErrors.Unauthorized);

        var liquidation = await _repository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), _currentUserService.CompanyId!, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(GetLiquidationByIdErrors.NotFound);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}

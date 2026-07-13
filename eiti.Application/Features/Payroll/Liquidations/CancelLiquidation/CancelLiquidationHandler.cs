using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public sealed class CancelLiquidationHandler : IRequestHandler<CancelLiquidationCommand, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly IPayrollBonusRepository _bonusRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelLiquidationHandler(
        ICurrentUserService currentUserService,
        IPayrollLiquidationRepository liquidationRepository,
        IPayrollAdvanceRepository advanceRepository,
        IPayrollBonusRepository bonusRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _liquidationRepository = liquidationRepository;
        _advanceRepository = advanceRepository;
        _bonusRepository = bonusRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(CancelLiquidationCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var liquidation = await _liquidationRepository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), companyId, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(CancelLiquidationErrors.NotFound);

        try
        {
            if (liquidation.PaymentMethod == PayrollPaymentMethod.Cash && liquidation.CashSessionId.HasValue)
            {
                var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(liquidation.CashSessionId.Value), companyId, cancellationToken);
                if (session is null)
                    return Result<PayrollLiquidationResponse>.Failure(CancelLiquidationErrors.NotFound);

                session.RegisterPayrollExpenseCancel(liquidation.NetAmount, liquidation.Id.Value, userId);
            }

            foreach (var advanceLine in liquidation.AdvanceLines)
            {
                var advance = await _advanceRepository.GetByIdAsync(new PayrollAdvanceId(advanceLine.PayrollAdvanceId), companyId, cancellationToken);
                advance?.Revert();
            }

            foreach (var bonusLine in liquidation.BonusLines)
            {
                var bonus = await _bonusRepository.GetByIdAsync(new PayrollBonusId(bonusLine.PayrollBonusId), companyId, cancellationToken);
                bonus?.RevertToPending();
            }

            liquidation.Cancel();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PayrollLiquidationResponse>.Failure(Error.Conflict("Payroll.Liquidations.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}

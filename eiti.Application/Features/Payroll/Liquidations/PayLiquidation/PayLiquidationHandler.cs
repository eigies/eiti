using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed class PayLiquidationHandler : IRequestHandler<PayLiquidationCommand, Result<PayrollLiquidationResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PayLiquidationHandler(
        ICurrentUserService currentUserService,
        IPayrollLiquidationRepository liquidationRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _liquidationRepository = liquidationRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollLiquidationResponse>> Handle(PayLiquidationCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollLiquidationResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var liquidation = await _liquidationRepository.GetByIdAsync(new PayrollLiquidationId(request.LiquidationId), companyId, cancellationToken);
        if (liquidation is null)
            return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.NotFound);

        var method = (PayrollPaymentMethod)request.PaymentMethod;

        if (method == PayrollPaymentMethod.Cash && request.CashSessionId is null)
            return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.CashSessionRequired);

        CashSession? session = null;
        if (method == PayrollPaymentMethod.Cash)
        {
            session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.CashSessionId!.Value), companyId, cancellationToken);
            if (session is null)
                return Result<PayrollLiquidationResponse>.Failure(PayLiquidationErrors.CashSessionNotFound);

            var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
                _currentUserService, _cashDrawerRepository, session.CashDrawerId, cancellationToken);
            if (accessCheck.IsFailure)
                return Result<PayrollLiquidationResponse>.Failure(accessCheck.Error!);
        }

        try
        {
            liquidation.MarkAsPaid(method, session?.Id.Value);

            if (method == PayrollPaymentMethod.Cash)
            {
                session!.RegisterPayrollExpense(liquidation.NetAmount, liquidation.Id.Value, userId);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PayrollLiquidationResponse>.Failure(Error.Conflict("Payroll.Liquidations.Pay.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollLiquidationResponse>.Success(PayrollLiquidationMapper.Map(liquidation));
    }
}

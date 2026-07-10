using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Cash;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public sealed class CancelPayrollAdvanceHandler : IRequestHandler<CancelPayrollAdvanceCommand, Result<PayrollAdvanceResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPayrollAdvanceHandler(
        ICurrentUserService currentUserService,
        IPayrollAdvanceRepository advanceRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _advanceRepository = advanceRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollAdvanceResponse>> Handle(CancelPayrollAdvanceCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollAdvanceResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var advance = await _advanceRepository.GetByIdAsync(new PayrollAdvanceId(request.Id), companyId, cancellationToken);
        if (advance is null)
            return Result<PayrollAdvanceResponse>.Failure(CancelPayrollAdvanceErrors.NotFound);

        try
        {
            if (advance.CashSessionId.HasValue)
            {
                var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(advance.CashSessionId.Value), companyId, cancellationToken);
                if (session is null)
                    return Result<PayrollAdvanceResponse>.Failure(CancelPayrollAdvanceErrors.CashSessionNotFound);

                session.RegisterPayrollAdvanceExpenseCancel(advance.Amount, advance.Id.Value, userId);
            }

            advance.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<PayrollAdvanceResponse>.Failure(Error.Conflict("Payroll.Advances.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollAdvanceResponse>.Success(
            new PayrollAdvanceResponse(advance.Id.Value, advance.EmployeeId.Value, advance.Amount, advance.Date, advance.Notes, (int)advance.Status, advance.AppliedToLiquidationId?.Value, advance.CashSessionId));
    }
}

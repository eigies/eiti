using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public sealed class CancelPayrollBonusHandler : IRequestHandler<CancelPayrollBonusCommand, Result<PayrollBonusResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPayrollBonusHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollBonusResponse>> Handle(CancelPayrollBonusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollBonusResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<PayrollBonusResponse>.Failure(CancelPayrollBonusErrors.Unauthorized);

        var bonus = await _repository.GetByIdAsync(new PayrollBonusId(request.Id), _currentUserService.CompanyId, cancellationToken);
        if (bonus is null)
            return Result<PayrollBonusResponse>.Failure(CancelPayrollBonusErrors.NotFound);

        try
        {
            bonus.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<PayrollBonusResponse>.Failure(Error.Conflict("Payroll.Bonuses.Cancel.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollBonusResponse>.Success(new PayrollBonusResponse(
            bonus.Id.Value, bonus.EmployeeId.Value, bonus.ConceptId.Value, (int)bonus.AmountType, bonus.Value, bonus.Notes, (int)bonus.Status, bonus.PayrollLiquidationId?.Value));
    }
}

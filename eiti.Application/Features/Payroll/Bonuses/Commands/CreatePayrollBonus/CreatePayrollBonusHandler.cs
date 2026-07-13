using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.Bonuses;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed class CreatePayrollBonusHandler : IRequestHandler<CreatePayrollBonusCommand, Result<PayrollBonusResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusRepository _bonusRepository;
    private readonly IPayrollBonusConceptRepository _conceptRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePayrollBonusHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusRepository bonusRepository,
        IPayrollBonusConceptRepository conceptRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _bonusRepository = bonusRepository;
        _conceptRepository = conceptRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollBonusResponse>> Handle(CreatePayrollBonusCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<PayrollBonusResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId;

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), companyId, cancellationToken);
        if (employee is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.EmployeeNotFound);

        var concept = await _conceptRepository.GetByIdAsync(new PayrollBonusConceptId(request.ConceptId), companyId, cancellationToken);
        if (concept is null)
            return Result<PayrollBonusResponse>.Failure(CreatePayrollBonusErrors.ConceptNotFound);

        var bonus = PayrollBonus.Create(
            companyId, employee.Id, concept.Id, (PayrollBonusAmountType)request.AmountType, request.Value, request.Notes);

        await _bonusRepository.AddAsync(bonus, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollBonusResponse>.Success(new PayrollBonusResponse(
            bonus.Id.Value, bonus.EmployeeId.Value, bonus.ConceptId.Value, (int)bonus.AmountType, bonus.Value, bonus.Notes, (int)bonus.Status, bonus.PayrollLiquidationId?.Value));
    }
}

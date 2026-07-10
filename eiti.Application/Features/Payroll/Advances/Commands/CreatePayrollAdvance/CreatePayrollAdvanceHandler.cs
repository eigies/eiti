using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using eiti.Domain.Cash;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed class CreatePayrollAdvanceHandler : IRequestHandler<CreatePayrollAdvanceCommand, Result<PayrollAdvanceResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePayrollAdvanceHandler(
        ICurrentUserService currentUserService,
        IPayrollAdvanceRepository advanceRepository,
        IEmployeeRepository employeeRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _advanceRepository = advanceRepository;
        _employeeRepository = employeeRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayrollAdvanceResponse>> Handle(CreatePayrollAdvanceCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<PayrollAdvanceResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var employee = await _employeeRepository.GetByIdAsync(new EmployeeId(request.EmployeeId), companyId, cancellationToken);
        if (employee is null)
            return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.EmployeeNotFound);

        var method = (PayrollPaymentMethod)request.PaymentMethod;

        if (method == PayrollPaymentMethod.Cash && request.CashSessionId is null)
            return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.CashSessionRequired);

        CashSession? session = null;
        if (method == PayrollPaymentMethod.Cash)
        {
            session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.CashSessionId!.Value), companyId, cancellationToken);
            if (session is null)
                return Result<PayrollAdvanceResponse>.Failure(CreatePayrollAdvanceErrors.CashSessionNotFound);

            var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
                _currentUserService, _cashDrawerRepository, session.CashDrawerId, cancellationToken);
            if (accessCheck.IsFailure)
                return Result<PayrollAdvanceResponse>.Failure(accessCheck.Error!);
        }

        var advance = PayrollAdvance.Create(companyId, employee.Id, request.Amount, request.Date, request.Notes, userId);

        if (method == PayrollPaymentMethod.Cash)
        {
            try
            {
                session!.RegisterPayrollAdvanceExpense(request.Amount, advance.Id.Value, userId);
            }
            catch (InvalidOperationException ex)
            {
                return Result<PayrollAdvanceResponse>.Failure(Error.Conflict("Payroll.Advances.Create.CashConflict", ex.Message));
            }
        }

        await _advanceRepository.AddAsync(advance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollAdvanceResponse>.Success(
            new PayrollAdvanceResponse(advance.Id.Value, advance.EmployeeId.Value, advance.Amount, advance.Date, advance.Notes, (int)advance.Status, advance.AppliedToLiquidationId?.Value));
    }
}

using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed class GeneratePayrollPeriodHandler : IRequestHandler<GeneratePayrollPeriodCommand, Result<GeneratePayrollPeriodResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPayrollDeductionConceptRepository _deductionConceptRepository;
    private readonly IPayrollAdvanceRepository _advanceRepository;
    private readonly IPayrollLiquidationRepository _liquidationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GeneratePayrollPeriodHandler(
        ICurrentUserService currentUserService,
        IEmployeeRepository employeeRepository,
        IPayrollDeductionConceptRepository deductionConceptRepository,
        IPayrollAdvanceRepository advanceRepository,
        IPayrollLiquidationRepository liquidationRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _deductionConceptRepository = deductionConceptRepository;
        _advanceRepository = advanceRepository;
        _liquidationRepository = liquidationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<GeneratePayrollPeriodResponse>> Handle(GeneratePayrollPeriodCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GeneratePayrollPeriodResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<GeneratePayrollPeriodResponse>.Failure(GeneratePayrollPeriodErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId!;
        var periodicity = (PayrollPeriodicity)request.Periodicity;

        var employees = await _employeeRepository.ListByCompanyAsync(companyId, cancellationToken);
        var activeConcepts = await _deductionConceptRepository.ListByCompanyAsync(companyId, activeOnly: true, cancellationToken);

        var generated = new List<PayrollLiquidationSummary>();
        var skipped = new List<GeneratePayrollPeriodSkippedItem>();

        foreach (var employee in employees.Where(e => e.IsActive))
        {
            if (employee.BaseSalary is null || employee.PayrollPeriodicity != periodicity)
            {
                skipped.Add(new GeneratePayrollPeriodSkippedItem(employee.Id.Value, employee.FullName, "Sin sueldo base configurado para esta periodicidad."));
                continue;
            }

            if (await _liquidationRepository.ExistsForPeriodAsync(companyId, employee.Id, request.PeriodLabel, cancellationToken))
            {
                skipped.Add(new GeneratePayrollPeriodSkippedItem(employee.Id.Value, employee.FullName, $"Ya tiene una liquidación para el período {request.PeriodLabel}."));
                continue;
            }

            var deductionLines = activeConcepts
                .Select(concept => PayrollLiquidationDeductionLine.Create(
                    concept.Name,
                    concept.Percentage,
                    decimal.Round(employee.BaseSalary.Value * concept.Percentage / 100m, 2, MidpointRounding.AwayFromZero)))
                .ToList();

            var pendingAdvances = await _advanceRepository.ListPendingByEmployeeAsync(companyId, employee.Id, cancellationToken);
            var advanceLines = pendingAdvances
                .Select(advance => PayrollLiquidationAdvanceLine.Create(advance.Id.Value, advance.Amount))
                .ToList();

            var liquidation = PayrollLiquidation.Create(
                companyId,
                employee.Id,
                employee.BranchId,
                request.PeriodLabel,
                request.PeriodStart,
                request.PeriodEnd,
                employee.BaseSalary.Value,
                deductionLines,
                advanceLines);

            foreach (var advance in pendingAdvances)
            {
                advance.Apply(liquidation.Id);
            }

            await _liquidationRepository.AddAsync(liquidation, cancellationToken);
            generated.Add(new PayrollLiquidationSummary(liquidation.Id.Value, employee.Id.Value, employee.FullName, liquidation.NetAmount));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<GeneratePayrollPeriodResponse>.Success(
            new GeneratePayrollPeriodResponse(generated.Count, generated, skipped));
    }
}

using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed class CreateDeductionConceptHandler : IRequestHandler<CreateDeductionConceptCommand, Result<DeductionConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDeductionConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollDeductionConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeductionConceptResponse>> Handle(CreateDeductionConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DeductionConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<DeductionConceptResponse>.Failure(CreateDeductionConceptErrors.Unauthorized);

        var concept = PayrollDeductionConcept.Create(_currentUserService.CompanyId, request.Name, request.Percentage);

        await _repository.AddAsync(concept, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeductionConceptResponse>.Success(
            new DeductionConceptResponse(concept.Id.Value, concept.Name, concept.Percentage, concept.IsActive));
    }
}

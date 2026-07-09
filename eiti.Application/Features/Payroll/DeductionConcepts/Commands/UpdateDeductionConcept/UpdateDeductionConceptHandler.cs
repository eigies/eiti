using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.UpdateDeductionConcept;

public sealed class UpdateDeductionConceptHandler : IRequestHandler<UpdateDeductionConceptCommand, Result<DeductionConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDeductionConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollDeductionConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeductionConceptResponse>> Handle(UpdateDeductionConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DeductionConceptResponse>.Failure(authCheck.Error);

        var concept = await _repository.GetByIdAsync(new PayrollDeductionConceptId(request.Id), _currentUserService.CompanyId!, cancellationToken);
        if (concept is null)
            return Result<DeductionConceptResponse>.Failure(UpdateDeductionConceptErrors.NotFound);

        concept.Update(request.Name, request.Percentage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DeductionConceptResponse>.Success(
            new DeductionConceptResponse(concept.Id.Value, concept.Name, concept.Percentage, concept.IsActive));
    }
}

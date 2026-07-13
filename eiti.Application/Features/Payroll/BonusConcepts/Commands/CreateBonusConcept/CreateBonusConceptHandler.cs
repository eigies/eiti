using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using eiti.Domain.Payroll;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed class CreateBonusConceptHandler : IRequestHandler<CreateBonusConceptCommand, Result<BonusConceptResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBonusConceptHandler(
        ICurrentUserService currentUserService,
        IPayrollBonusConceptRepository repository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BonusConceptResponse>> Handle(CreateBonusConceptCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BonusConceptResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BonusConceptResponse>.Failure(CreateBonusConceptErrors.Unauthorized);

        var concept = PayrollBonusConcept.Create(_currentUserService.CompanyId, request.Name);

        await _repository.AddAsync(concept, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BonusConceptResponse>.Success(new BonusConceptResponse(concept.Id.Value, concept.Name, concept.IsActive));
    }
}

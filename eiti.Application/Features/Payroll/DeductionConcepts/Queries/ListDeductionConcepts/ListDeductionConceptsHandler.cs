using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Queries.ListDeductionConcepts;

public sealed class ListDeductionConceptsHandler : IRequestHandler<ListDeductionConceptsQuery, Result<IReadOnlyList<DeductionConceptResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollDeductionConceptRepository _repository;

    public ListDeductionConceptsHandler(ICurrentUserService currentUserService, IPayrollDeductionConceptRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<DeductionConceptResponse>>> Handle(ListDeductionConceptsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<DeductionConceptResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<DeductionConceptResponse>>.Failure(ListDeductionConceptsErrors.Unauthorized);

        var concepts = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, request.ActiveOnly, cancellationToken);

        IReadOnlyList<DeductionConceptResponse> items = concepts
            .Select(c => new DeductionConceptResponse(c.Id.Value, c.Name, c.Percentage, c.IsActive))
            .ToList();

        return Result<IReadOnlyList<DeductionConceptResponse>>.Success(items);
    }
}

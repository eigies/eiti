using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Queries.ListBonusConcepts;

public sealed class ListBonusConceptsHandler : IRequestHandler<ListBonusConceptsQuery, Result<IReadOnlyList<BonusConceptResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPayrollBonusConceptRepository _repository;

    public ListBonusConceptsHandler(ICurrentUserService currentUserService, IPayrollBonusConceptRepository repository)
    {
        _currentUserService = currentUserService;
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<BonusConceptResponse>>> Handle(ListBonusConceptsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<BonusConceptResponse>>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<IReadOnlyList<BonusConceptResponse>>.Failure(ListBonusConceptsErrors.Unauthorized);

        var concepts = await _repository.ListByCompanyAsync(_currentUserService.CompanyId, request.ActiveOnly, cancellationToken);

        IReadOnlyList<BonusConceptResponse> items = concepts
            .Select(c => new BonusConceptResponse(c.Id.Value, c.Name, c.IsActive))
            .ToList();

        return Result<IReadOnlyList<BonusConceptResponse>>.Success(items);
    }
}

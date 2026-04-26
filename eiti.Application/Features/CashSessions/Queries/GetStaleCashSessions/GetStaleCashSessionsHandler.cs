using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Companies;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetStaleCashSessions;

public sealed class GetStaleCashSessionsHandler
    : IRequestHandler<GetStaleCashSessionsQuery, Result<IReadOnlyList<StaleCashSessionResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;

    public GetStaleCashSessionsHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<IReadOnlyList<StaleCashSessionResponse>>> Handle(
        GetStaleCashSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<StaleCashSessionResponse>>.Failure(authCheck.Error);

        var companyId = new CompanyId(_currentUserService.CompanyId!.Value);
        var threshold = DateTime.UtcNow.AddHours(-request.HoursThreshold);

        var sessions = await _cashSessionRepository.GetAllStaleOpenAsync(companyId, threshold, cancellationToken);

        var result = sessions.Select(s =>
        {
            var hoursOpen = (int)Math.Floor((DateTime.UtcNow - s.OpenedAt).TotalHours);
            return new StaleCashSessionResponse(s.Id.Value, s.CashDrawerId.Value, s.OpenedAt, hoursOpen);
        }).ToList();

        return Result<IReadOnlyList<StaleCashSessionResponse>>.Success(result);
    }
}

using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCashSessionSummary;

public sealed class GetCashSessionSummaryHandler : IRequestHandler<GetCashSessionSummaryQuery, Result<CashSessionSummaryResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IBankRepository _bankRepository;

    public GetCashSessionSummaryHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _bankRepository = bankRepository;
    }

    public async Task<Result<CashSessionSummaryResponse>> Handle(GetCashSessionSummaryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashSessionSummaryResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.Id), companyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionSummaryResponse>.Failure(Error.NotFound("CashSessions.Summary.NotFound", "The requested cash session was not found."));
        }

        var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            session.CashDrawerId,
            cancellationToken);
        if (accessCheck.IsFailure)
            return Result<CashSessionSummaryResponse>.Failure(accessCheck.Error!);

        var payments = await _saleRepository.GetPaymentsByCashSessionIdAsync(session.Id, cancellationToken);

        var banks = await _bankRepository.ListAsync(activeOnly: false, companyId, cancellationToken);
        var bankNames = banks.ToDictionary(b => b.Id, b => b.Name);

        return Result<CashSessionSummaryResponse>.Success(CashSessionMapper.MapSummary(session, payments, bankNames));
    }
}

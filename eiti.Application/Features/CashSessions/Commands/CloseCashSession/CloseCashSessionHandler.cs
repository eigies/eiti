using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CloseCashSession;

public sealed class CloseCashSessionHandler : IRequestHandler<CloseCashSessionCommand, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashSessionResponse>> Handle(CloseCashSessionCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(authCheck.Error);

        var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(CloseCashSessionErrors.NotFound);
        }

        var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            session.CashDrawerId,
            cancellationToken);
        if (accessCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(accessCheck.Error!);

        var hasPendingOnHoldSales = await _saleRepository.HasOnHoldSalesByCashDrawerAsync(
            _currentUserService.CompanyId,
            session.CashDrawerId,
            cancellationToken);
        if (hasPendingOnHoldSales)
            return Result<CashSessionResponse>.Failure(CloseCashSessionErrors.PendingOnHoldSales);

        try
        {
            session.Close(request.ActualClosingAmount, _currentUserService.UserId, request.Notes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CashSessionResponse>.Failure(Error.Conflict("CashSessions.Close.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session));
    }
}

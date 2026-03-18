using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CloseCashSession;

public sealed class CloseCashSessionHandler : IRequestHandler<CloseCashSessionCommand, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CloseCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
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

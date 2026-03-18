using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashTransfer;

public sealed class CreateCashTransferHandler : IRequestHandler<CreateCashTransferCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCashTransferHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CreateCashTransferCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        if (request.SourceCashDrawerId == request.TargetCashDrawerId)
            return Result.Failure(CashTransferErrors.SameSession);

        var sourceSession = await _cashSessionRepository.GetOpenByDrawerAsync(
            new CashDrawerId(request.SourceCashDrawerId),
            _currentUserService.CompanyId,
            cancellationToken);

        if (sourceSession is null)
            return Result.Failure(CashTransferErrors.SourceSessionNotFound);

        var targetSession = await _cashSessionRepository.GetOpenByDrawerAsync(
            new CashDrawerId(request.TargetCashDrawerId),
            _currentUserService.CompanyId,
            cancellationToken);

        if (targetSession is null)
            return Result.Failure(CashTransferErrors.TargetSessionNotFound);

        try
        {
            sourceSession.RegisterTransferOut(
                request.Amount,
                targetSession.Id.Value,
                request.Description,
                _currentUserService.UserId);

            targetSession.RegisterTransferIn(
                request.Amount,
                sourceSession.Id.Value,
                request.Description,
                _currentUserService.UserId);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(Error.Conflict("CashTransfer.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashWithdrawal;

public sealed class CreateCashWithdrawalHandler : IRequestHandler<CreateCashWithdrawalCommand, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCashWithdrawalHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashSessionResponse>> Handle(CreateCashWithdrawalCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<CashSessionResponse>.Failure(Error.Unauthorized("CashSessions.Withdraw.Unauthorized", "The current user is not authenticated."));
        }

        var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Withdraw.NotFound", "The requested cash session was not found."));
        }

        try
        {
            session.RegisterWithdrawal(request.Amount, request.Description, _currentUserService.UserId);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CashSessionResponse>.Failure(Error.Conflict("CashSessions.Withdraw.InvalidOperation", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session));
    }
}

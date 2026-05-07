using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.CreateCashWithdrawal;

public sealed class CreateCashWithdrawalHandler : IRequestHandler<CreateCashWithdrawalCommand, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCashWithdrawalHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashSessionResponse>> Handle(CreateCashWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(authCheck.Error);

        var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Withdraw.NotFound", "The requested cash session was not found."));
        }

        var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            session.CashDrawerId,
            cancellationToken);
        if (accessCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(accessCheck.Error!);

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

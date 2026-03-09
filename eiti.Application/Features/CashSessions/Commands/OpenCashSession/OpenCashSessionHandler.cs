using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Commands.OpenCashSession;

public sealed class OpenCashSessionHandler : IRequestHandler<OpenCashSessionCommand, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenCashSessionHandler(
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

    public async Task<Result<CashSessionResponse>> Handle(OpenCashSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<CashSessionResponse>.Failure(Error.Unauthorized("CashSessions.Open.Unauthorized", "The current user is not authenticated."));
        }

        var drawer = await _cashDrawerRepository.GetByIdAsync(new CashDrawerId(request.CashDrawerId), _currentUserService.CompanyId, cancellationToken);

        if (drawer is null || !drawer.IsActive)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Open.CashDrawerNotFound", "The requested cash drawer was not found."));
        }

        var existing = await _cashSessionRepository.GetOpenByDrawerAsync(drawer.Id, _currentUserService.CompanyId, cancellationToken);

        if (existing is not null)
        {
            return Result<CashSessionResponse>.Failure(Error.Conflict("CashSessions.Open.AlreadyOpen", "The cash drawer already has an open session."));
        }

        CashSession session;
        try
        {
            session = CashSession.Open(_currentUserService.CompanyId, drawer.BranchId, drawer.Id, _currentUserService.UserId, request.OpeningAmount, request.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result<CashSessionResponse>.Failure(Error.Validation("CashSessions.Open.InvalidInput", ex.Message));
        }

        await _cashSessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session));
    }
}

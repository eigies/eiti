using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashDrawers.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Commands.UpdateCashDrawer;

public sealed class UpdateCashDrawerHandler : IRequestHandler<UpdateCashDrawerCommand, Result<CashDrawerResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCashDrawerHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CashDrawerResponse>> Handle(UpdateCashDrawerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashDrawerResponse>.Failure(authCheck.Error);

        var drawer = await _cashDrawerRepository.GetByIdAsync(new CashDrawerId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (drawer is null)
        {
            return Result<CashDrawerResponse>.Failure(Error.NotFound("CashDrawers.Update.NotFound", "The requested cash drawer was not found."));
        }

        if (await _cashDrawerRepository.NameExistsAsync(drawer.BranchId, request.Name.Trim(), drawer.Id, cancellationToken))
        {
            return Result<CashDrawerResponse>.Failure(Error.Conflict("CashDrawers.Update.NameAlreadyExists", "A cash drawer with the same name already exists."));
        }

        try
        {
            drawer.Update(request.Name, request.IsActive);
        }
        catch (ArgumentException ex)
        {
            return Result<CashDrawerResponse>.Failure(Error.Validation("CashDrawers.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CashDrawerResponse>.Success(new(drawer.Id.Value, drawer.BranchId.Value, drawer.Name, drawer.IsActive, drawer.CreatedAt, drawer.UpdatedAt, drawer.AssignedUserId?.Value));
    }
}

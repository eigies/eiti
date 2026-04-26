using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.CashDrawers.Commands.AssignCashDrawer;

public sealed class AssignCashDrawerHandler : IRequestHandler<AssignCashDrawerCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignCashDrawerHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AssignCashDrawerCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return authCheck;

        var companyId = new CompanyId(_currentUserService.CompanyId!.Value);

        var drawer = await _cashDrawerRepository.GetByIdAsync(
            new CashDrawerId(request.CashDrawerId),
            companyId,
            cancellationToken);

        if (drawer is null)
            return Result.Failure(AssignCashDrawerErrors.DrawerNotFound);

        UserId? assignedUserId = null;

        if (request.UserId.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(new UserId(request.UserId.Value), cancellationToken);

            if (user is null || user.CompanyId != companyId)
                return Result.Failure(AssignCashDrawerErrors.UserNotFound);

            assignedUserId = user.Id;
        }

        drawer.Assign(assignedUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

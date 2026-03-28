using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Banks.Queries.ListBanks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.UpsertInstallmentPlan;

public sealed class UpsertInstallmentPlanHandler : IRequestHandler<UpsertInstallmentPlanCommand, Result<BankResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBankRepository _bankRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertInstallmentPlanHandler(
        ICurrentUserService currentUserService,
        IBankRepository bankRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _bankRepository = bankRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BankResponse>> Handle(UpsertInstallmentPlanCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BankResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BankResponse>.Failure(UpsertInstallmentPlanErrors.Unauthorized);

        var bank = await _bankRepository.GetByIdAsync(request.BankId, cancellationToken);
        if (bank is null)
            return Result<BankResponse>.Failure(UpsertInstallmentPlanErrors.NotFound);

        try
        {
            bank.UpsertInstallmentPlan(request.Cuotas, request.SurchargePct, request.Active);
        }
        catch (ArgumentException)
        {
            return Result<BankResponse>.Failure(UpsertInstallmentPlanErrors.InvalidCuotas);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var plans = bank.InstallmentPlans
            .Select(p => new BankInstallmentPlanResponse(p.Id, p.Cuotas, p.SurchargePct, p.Active))
            .ToList();

        return Result<BankResponse>.Success(new BankResponse(bank.Id, bank.Name, bank.Active, plans));
    }
}

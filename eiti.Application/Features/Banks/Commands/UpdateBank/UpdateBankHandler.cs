using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Banks.Queries.ListBanks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.UpdateBank;

public sealed class UpdateBankHandler : IRequestHandler<UpdateBankCommand, Result<BankResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBankRepository _bankRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBankHandler(
        ICurrentUserService currentUserService,
        IBankRepository bankRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _bankRepository = bankRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BankResponse>> Handle(UpdateBankCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BankResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BankResponse>.Failure(UpdateBankErrors.Unauthorized);

        var companyId = _currentUserService.CompanyId!;
        var bank = await _bankRepository.GetByIdAsync(request.Id, companyId, cancellationToken);
        if (bank is null)
            return Result<BankResponse>.Failure(UpdateBankErrors.NotFound);

        bank.Update(request.Name, request.Active);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var plans = bank.InstallmentPlans
            .Select(p => new BankInstallmentPlanResponse(p.Id, p.Cuotas, p.SurchargePct, p.Active))
            .ToList();

        return Result<BankResponse>.Success(new BankResponse(bank.Id, bank.Name, bank.Active, plans));
    }
}

using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Banks.Queries.ListBanks;
using eiti.Domain.Banks;
using MediatR;

namespace eiti.Application.Features.Banks.Commands.CreateBank;

public sealed class CreateBankHandler : IRequestHandler<CreateBankCommand, Result<BankResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBankRepository _bankRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBankHandler(
        ICurrentUserService currentUserService,
        IBankRepository bankRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _bankRepository = bankRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BankResponse>> Handle(CreateBankCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BankResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BankResponse>.Failure(CreateBankErrors.Unauthorized);

        var bank = Bank.Create(request.Name);

        await _bankRepository.AddAsync(bank, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BankResponse>.Success(new BankResponse(bank.Id, bank.Name, bank.Active, []));
    }
}

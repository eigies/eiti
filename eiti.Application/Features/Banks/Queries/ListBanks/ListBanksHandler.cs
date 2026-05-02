using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Banks.Queries.ListBanks;

public sealed class ListBanksHandler : IRequestHandler<ListBanksQuery, Result<IReadOnlyList<BankResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBankRepository _bankRepository;

    public ListBanksHandler(ICurrentUserService currentUserService, IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _bankRepository = bankRepository;
    }

    public async Task<Result<IReadOnlyList<BankResponse>>> Handle(ListBanksQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<BankResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var banks = await _bankRepository.ListAsync(request.ActiveOnly, companyId, cancellationToken);

        var response = banks
            .Select(b => new BankResponse(
                b.Id,
                b.Name,
                b.Active,
                b.InstallmentPlans
                    .Select(p => new BankInstallmentPlanResponse(p.Id, p.Cuotas, p.SurchargePct, p.Active))
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<BankResponse>>.Success(response);
    }
}

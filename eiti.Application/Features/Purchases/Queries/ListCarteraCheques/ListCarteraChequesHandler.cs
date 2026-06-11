using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Purchases.Queries.ListCarteraCheques;

public sealed class ListCarteraChequesHandler
    : IRequestHandler<ListCarteraChequesQuery, Result<IReadOnlyList<CarteraChequeResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;

    public ListCarteraChequesHandler(
        ICurrentUserService currentUserService,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository)
    {
        _currentUserService = currentUserService;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
    }

    public async Task<Result<IReadOnlyList<CarteraChequeResponse>>> Handle(
        ListCarteraChequesQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<CarteraChequeResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var filters = new ChequeFilters(ChequeStatus.EnCartera, null, null, null);
        var cheques = await _chequeRepository.ListAsync(filters, companyId, cancellationToken);

        var allBanks = await _bankRepository.ListAsync(false, companyId, cancellationToken);
        var bankNameById = allBanks.ToDictionary(b => b.Id, b => b.Name);

        var response = cheques
            .Select(c => new CarteraChequeResponse(
                c.Id,
                c.Numero,
                c.Titular,
                c.CuitDni,
                c.Monto,
                c.BankId,
                bankNameById.TryGetValue(c.BankId, out var name) ? name : "Unknown",
                c.FechaVencimiento))
            .ToList();

        return Result<IReadOnlyList<CarteraChequeResponse>>.Success(response);
    }
}

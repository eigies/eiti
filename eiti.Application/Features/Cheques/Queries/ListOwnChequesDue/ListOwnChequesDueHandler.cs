using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.ListOwnChequesDue;

public sealed class ListOwnChequesDueHandler : IRequestHandler<ListOwnChequesDueQuery, Result<IReadOnlyList<OwnChequeDueResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly ISupplierRepository _supplierRepository;

    public ListOwnChequesDueHandler(
        ICurrentUserService currentUserService,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        ISupplierRepository supplierRepository)
    {
        _currentUserService = currentUserService;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<IReadOnlyList<OwnChequeDueResponse>>> Handle(ListOwnChequesDueQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<OwnChequeDueResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var (from, to) = OwnChequeDueWindow.For(BusinessCalendar.ToBusinessDate(DateTime.UtcNow));

        // Entregado = vigente: un cheque propio anulado no se debita.
        var cheques = await _chequeRepository.ListAsync(
            new ChequeFilters(ChequeStatus.Entregado, null, from, to, null, ChequeOrigen.Propio), companyId, cancellationToken);
        if (cheques.Count == 0)
            return Result<IReadOnlyList<OwnChequeDueResponse>>.Success([]);

        var bankNames = (await _bankRepository.ListAsync(false, companyId, cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var supplierNames = (await _supplierRepository.ListAsync(companyId.Value, false, null, cancellationToken)).ToDictionary(s => s.Id, s => s.Name);

        return Result<IReadOnlyList<OwnChequeDueResponse>>.Success(cheques
            .Select(c => new OwnChequeDueResponse(
                c.Id,
                c.Numero,
                bankNames.GetValueOrDefault(c.BankId, "Unknown"),
                c.SupplierId is { } supplierId ? supplierNames.GetValueOrDefault(supplierId) : null,
                c.Monto,
                c.FechaVencimiento))
            .ToList());
    }
}

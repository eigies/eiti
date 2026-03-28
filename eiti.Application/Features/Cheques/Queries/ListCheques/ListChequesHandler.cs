using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.ListCheques;

public sealed class ListChequesHandler : IRequestHandler<ListChequesQuery, Result<IReadOnlyList<ChequeListItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly ISaleRepository _saleRepository;

    public ListChequesHandler(
        ICurrentUserService currentUserService,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        ISaleRepository saleRepository)
    {
        _currentUserService = currentUserService;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _saleRepository = saleRepository;
    }

    public async Task<Result<IReadOnlyList<ChequeListItemResponse>>> Handle(ListChequesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ChequeListItemResponse>>.Failure(authCheck.Error);

        var filters = new ChequeFilters(request.Estado, request.BankId, request.FechaVencFrom, request.FechaVencTo);
        var cheques = await _chequeRepository.ListAsync(filters, cancellationToken);

        // Fetch all banks once for name lookup
        var allBanks = await _bankRepository.ListAsync(false, cancellationToken);
        var bankNameById = allBanks.ToDictionary(b => b.Id, b => b.Name);

        // Collect unique sale IDs for regular payments
        var regularSaleIds = cheques
            .Where(c => c.SalePaymentSaleId.HasValue)
            .Select(c => c.SalePaymentSaleId!.Value)
            .Distinct()
            .ToList();

        // Collect unique CC payment IDs to resolve their sale IDs
        var ccPaymentIds = cheques
            .Where(c => c.SaleCcPaymentId.HasValue)
            .Select(c => c.SaleCcPaymentId!.Value)
            .Distinct()
            .ToList();

        // Build saleId → saleCode dictionary
        var saleCodes = new Dictionary<Guid, string?>();

        if (regularSaleIds.Count > 0)
        {
            var regularSales = await _saleRepository.GetByIdsAsync(regularSaleIds, cancellationToken);
            foreach (var sale in regularSales)
            {
                saleCodes[sale.Id.Value] = sale.Code;
            }
        }

        if (ccPaymentIds.Count > 0)
        {
            var ccPaymentSaleIds = await _saleRepository.GetSaleIdsByCcPaymentIdsAsync(ccPaymentIds, cancellationToken);
            var ccSaleIds = ccPaymentSaleIds.Values.Distinct().ToList();
            if (ccSaleIds.Count > 0)
            {
                var ccSales = await _saleRepository.GetByIdsAsync(ccSaleIds, cancellationToken);
                foreach (var sale in ccSales)
                {
                    saleCodes[sale.Id.Value] = sale.Code;
                }
            }

            // Map ccPaymentId → saleCode via ccPaymentId → saleId → saleCode
            foreach (var (ccPaymentId, saleId) in ccPaymentSaleIds)
            {
                saleCodes.TryGetValue(saleId, out var code);
                saleCodes[ccPaymentId] = code; // keyed by ccPaymentId for easy lookup below
            }
        }

        var response = cheques.Select(c =>
        {
            var bankName = bankNameById.TryGetValue(c.BankId, out var name) ? name : "Unknown";
            string? saleCode;
            string saleType;

            if (c.SaleCcPaymentId.HasValue)
            {
                saleType = "CC";
                saleCodes.TryGetValue(c.SaleCcPaymentId.Value, out saleCode);
            }
            else if (c.SalePaymentSaleId.HasValue)
            {
                saleType = "Regular";
                saleCodes.TryGetValue(c.SalePaymentSaleId.Value, out saleCode);
            }
            else
            {
                saleType = "Unknown";
                saleCode = null;
            }

            return new ChequeListItemResponse(
                c.Id,
                c.Numero,
                bankName,
                c.Titular,
                c.Monto,
                c.FechaVencimiento,
                (int)c.Estado,
                c.Estado.ToString(),
                saleCode,
                saleType);
        }).ToList();

        return Result<IReadOnlyList<ChequeListItemResponse>>.Success(response);
    }
}

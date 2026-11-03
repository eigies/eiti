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
    private readonly ISupplierRepository _supplierRepository;

    public ListChequesHandler(
        ICurrentUserService currentUserService,
        IChequeRepository chequeRepository,
        IBankRepository bankRepository,
        ISaleRepository saleRepository,
        ISupplierRepository supplierRepository)
    {
        _currentUserService = currentUserService;
        _chequeRepository = chequeRepository;
        _bankRepository = bankRepository;
        _saleRepository = saleRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<IReadOnlyList<ChequeListItemResponse>>> Handle(ListChequesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ChequeListItemResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var filters = new ChequeFilters(request.Estado, request.BankId, request.FechaVencFrom, request.FechaVencTo, request.Numero, request.Origen);
        var cheques = await _chequeRepository.ListAsync(filters, companyId, cancellationToken);

        var allBanks = await _bankRepository.ListAsync(false, companyId, cancellationToken);
        var bankNameById = allBanks.ToDictionary(b => b.Id, b => b.Name);

        var regularSaleIds = cheques
            .Where(c => c.SalePaymentSaleId.HasValue)
            .Select(c => c.SalePaymentSaleId!.Value)
            .Distinct()
            .ToList();

        var customerPaymentIds = cheques
            .Where(c => c.SaleCcPaymentId.HasValue)
            .Select(c => c.SaleCcPaymentId!.Value)
            .Distinct()
            .ToList();

        var saleCodes = new Dictionary<Guid, string?>();

        if (regularSaleIds.Count > 0)
        {
            var regularSales = await _saleRepository.GetByIdsAsync(regularSaleIds, cancellationToken);
            foreach (var sale in regularSales)
            {
                saleCodes[sale.Id.Value] = sale.Code;
            }
        }

        if (customerPaymentIds.Count > 0)
        {
            var customerPaymentSaleCodes = await _saleRepository.GetCodesByCustomerPaymentIdsAsync(customerPaymentIds, cancellationToken);
            foreach (var (customerPaymentId, code) in customerPaymentSaleCodes)
            {
                saleCodes[customerPaymentId] = code;
            }
        }

        // Los propios no vienen de una venta: muestran el proveedor al que se le entregaron.
        var supplierNames = cheques.Any(c => c.SupplierId.HasValue)
            ? (await _supplierRepository.ListAsync(companyId.Value, false, null, cancellationToken)).ToDictionary(s => s.Id, s => s.Name)
            : new Dictionary<Guid, string>();

        var response = cheques.Select(c =>
        {
            var bankName = bankNameById.TryGetValue(c.BankId, out var name) ? name : "Unknown";
            string? saleCode;
            string saleType;

            if (c.Origen == ChequeOrigen.Propio)
            {
                saleType = "Propio";
                saleCode = null;
            }
            else if (c.SaleCcPaymentId.HasValue)
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
                saleType,
                (int)c.Origen,
                c.SupplierId is { } supplierId ? supplierNames.GetValueOrDefault(supplierId) : null);
        }).ToList();

        return Result<IReadOnlyList<ChequeListItemResponse>>.Success(response);
    }
}

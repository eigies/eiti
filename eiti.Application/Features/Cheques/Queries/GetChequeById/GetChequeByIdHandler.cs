using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cheques;
using MediatR;

namespace eiti.Application.Features.Cheques.Queries.GetChequeById;

public sealed class GetChequeByIdHandler : IRequestHandler<GetChequeByIdQuery, Result<ChequeDetailResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IChequeRepository _chequeRepository;
    private readonly IBankRepository _bankRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly ISupplierRepository _supplierRepository;

    public GetChequeByIdHandler(
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

    public async Task<Result<ChequeDetailResponse>> Handle(GetChequeByIdQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ChequeDetailResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var cheque = await _chequeRepository.GetByIdAsync(request.Id, companyId, cancellationToken);
        if (cheque is null)
            return Result<ChequeDetailResponse>.Failure(
                Error.NotFound("Cheques.GetById.NotFound", "The cheque was not found."));

        var bank = await _bankRepository.GetByIdAsync(cheque.BankId, companyId, cancellationToken);
        var bankName = bank?.Name ?? "Unknown";

        string? saleCode = null;
        string saleType;
        string? supplierName = null;

        if (cheque.Origen == ChequeOrigen.Propio)
        {
            saleType = "Propio";
            var supplier = cheque.SupplierId is { } supplierId
                ? await _supplierRepository.GetByIdAsync(supplierId, companyId.Value, cancellationToken)
                : null;
            supplierName = supplier?.Name;
        }
        else if (cheque.SaleCcPaymentId.HasValue)
        {
            saleType = "CC";
            var customerPaymentSaleCodes = await _saleRepository.GetCodesByCustomerPaymentIdsAsync(
                [cheque.SaleCcPaymentId.Value], cancellationToken);
            if (customerPaymentSaleCodes.TryGetValue(cheque.SaleCcPaymentId.Value, out var code))
            {
                saleCode = code;
            }
        }
        else if (cheque.SalePaymentSaleId.HasValue)
        {
            saleType = "Regular";
            var sales = await _saleRepository.GetByIdsAsync([cheque.SalePaymentSaleId.Value], cancellationToken);
            saleCode = sales.FirstOrDefault()?.Code;
        }
        else
        {
            saleType = "Unknown";
        }

        return Result<ChequeDetailResponse>.Success(new ChequeDetailResponse(
            cheque.Id,
            cheque.Numero,
            cheque.BankId,
            bankName,
            cheque.Titular,
            cheque.CuitDni,
            cheque.Monto,
            cheque.FechaEmision,
            cheque.FechaVencimiento,
            (int)cheque.Estado,
            cheque.Estado.ToString(),
            cheque.Notas,
            saleCode,
            saleType,
            cheque.CreatedAt,
            cheque.UpdatedAt,
            (int)cheque.Origen,
            supplierName));
    }
}

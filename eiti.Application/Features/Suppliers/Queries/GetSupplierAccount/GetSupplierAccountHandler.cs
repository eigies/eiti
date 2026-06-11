using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;

public sealed class GetSupplierAccountHandler
    : IRequestHandler<GetSupplierAccountQuery, Result<GetSupplierAccountResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IChequeRepository _chequeRepository;

    public GetSupplierAccountHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        IPurchaseRepository purchaseRepository,
        IChequeRepository chequeRepository)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _purchaseRepository = purchaseRepository;
        _chequeRepository = chequeRepository;
    }

    public async Task<Result<GetSupplierAccountResponse>> Handle(
        GetSupplierAccountQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<GetSupplierAccountResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result<GetSupplierAccountResponse>.Failure(GetSupplierAccountErrors.SupplierNotFound);

        var purchases = await _purchaseRepository.ListAllBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);
        var payments = await _supplierPaymentRepository.ListBySupplierAsync(companyId.Value, supplier.Id, cancellationToken);

        // Números de cheque para los pagos que endosaron un cheque.
        var chequeNumeroById = new Dictionary<Guid, string>();
        foreach (var chequeId in payments.Where(p => p.ChequeId.HasValue).Select(p => p.ChequeId!.Value).Distinct())
        {
            var cheque = await _chequeRepository.GetByIdAsync(chequeId, companyId, cancellationToken);
            if (cheque is not null)
                chequeNumeroById[chequeId] = cheque.Numero;
        }

        var deudaTotal = purchases
            .Where(p => p.Status != PurchaseStatus.Cancelled)
            .Sum(p => p.GrandTotal);
        var saldoPendiente = purchases
            .Where(p => p.Status == PurchaseStatus.Active)
            .Sum(p => p.PendingAmount);
        var pagadoTotal = deudaTotal - saldoPendiente;

        var movements = new List<SupplierAccountMovement>();

        foreach (var p in purchases)
        {
            movements.Add(new SupplierAccountMovement(
                "compra",
                p.Id,
                p.CreatedAt,
                string.IsNullOrWhiteSpace(p.InvoiceNumber) ? "Compra" : $"Factura {p.InvoiceNumber}",
                p.Code,
                p.GrandTotal,
                true,
                (int)p.Status,
                p.Status.ToString(),
                null,
                null));

            // Pagos legacy (cargados por-compra antes del modelo de cuenta): se muestran como créditos.
            foreach (var legacy in p.Payments.Where(pay =>
                pay.SupplierPaymentId is null &&
                pay.Method != PurchasePaymentMethod.SupplierCredit &&
                pay.Status == PurchasePaymentStatus.Active))
            {
                movements.Add(new SupplierAccountMovement(
                    "pago",
                    legacy.Id,
                    legacy.Date,
                    "Pago (compra)",
                    p.Code,
                    legacy.Amount,
                    false,
                    (int)legacy.Status,
                    legacy.Status.ToString(),
                    legacy.Method.ToString(),
                    null));
            }
        }

        foreach (var pay in payments)
        {
            var chequeNumero = pay.ChequeId.HasValue && chequeNumeroById.TryGetValue(pay.ChequeId.Value, out var num)
                ? num
                : null;
            movements.Add(new SupplierAccountMovement(
                "pago",
                pay.Id,
                pay.Date,
                chequeNumero is not null ? $"Pago · Cheque #{chequeNumero}" : "Pago",
                null,
                pay.Amount,
                false,
                (int)pay.Status,
                pay.Status.ToString(),
                pay.Method.ToString(),
                chequeNumero));
        }

        var ordered = movements
            .OrderByDescending(m => m.Date)
            .ToList();

        return Result<GetSupplierAccountResponse>.Success(new GetSupplierAccountResponse(
            supplier.Id,
            supplier.Name,
            supplier.Phone,
            supplier.Email,
            deudaTotal,
            pagadoTotal,
            saldoPendiente,
            supplier.CreditBalance,
            ordered));
    }
}

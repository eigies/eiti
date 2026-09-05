using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Purchases.Common;
using MediatR;

namespace eiti.Application.Features.Suppliers.Queries.GetSupplierAccount;

public sealed record GetSupplierAccountQuery(Guid SupplierId)
    : IRequest<Result<GetSupplierAccountResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesAccess];
}

public sealed record GetSupplierAccountResponse(
    Guid SupplierId,
    string SupplierName,
    string? Phone,
    string? Email,
    decimal DeudaTotal,
    decimal PagadoTotal,
    decimal SaldoPendiente,
    decimal SaldoAFavor,
    IReadOnlyList<SupplierAccountMovement> Movements);

public sealed record SupplierAccountMovement(
    string Type,            // "compra" | "pago" | "nota_credito"
    Guid Id,
    DateTime Date,
    string Description,
    string? Code,           // COMP-XXXX en compras
    decimal Amount,
    bool IsDebit,
    int Status,
    string StatusName,
    string? Method,
    string? ChequeNumero,
    IReadOnlyList<SupplierPaymentImputacion>? Imputaciones,  // qué facturas cubrió este pago
    decimal? Sobrante,                                      // excedente del pago a saldo a favor
    string? Reference = null,                               // referencia ingresada al registrar el pago
    string? Notes = null);                                  // nota ingresada al registrar el pago

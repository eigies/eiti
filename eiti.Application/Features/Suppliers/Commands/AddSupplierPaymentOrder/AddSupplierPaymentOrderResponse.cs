using eiti.Application.Features.Purchases.Common;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPaymentOrder;

public sealed record AddSupplierPaymentOrderResponse(
    Guid PaymentOrderId,
    string Code,
    Guid SupplierId,
    decimal TotalAmount,
    IReadOnlyList<Guid> PaymentIds,
    decimal AppliedToPurchases,
    decimal CreditAdded,
    decimal SupplierCreditBalance,
    IReadOnlyList<SupplierPaymentImputacion> Imputaciones);

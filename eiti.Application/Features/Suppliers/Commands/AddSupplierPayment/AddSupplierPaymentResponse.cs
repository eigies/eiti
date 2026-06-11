namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public sealed record AddSupplierPaymentResponse(
    Guid PaymentId,
    Guid SupplierId,
    decimal Amount,
    decimal AppliedToPurchases,
    decimal CreditAdded,
    decimal SupplierCreditBalance);

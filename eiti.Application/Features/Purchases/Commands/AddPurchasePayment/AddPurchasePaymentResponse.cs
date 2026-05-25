namespace eiti.Application.Features.Purchases.Commands.AddPurchasePayment;

public sealed record AddPurchasePaymentResponse(
    Guid PurchaseId,
    int Status,
    string StatusName,
    decimal TotalAmount,
    decimal TotalPaid,
    decimal PendingAmount,
    Guid PaymentId);

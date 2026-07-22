namespace eiti.Application.Features.Sales.Queries.GetSaleById;

public sealed record GetSaleByIdResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid? CustomerId,
    string? CustomerFullName,
    string? CustomerDocument,
    string? CustomerTaxId,
    Guid? CashDrawerId,
    bool HasDelivery,
    int IdSaleStatus,
    string SaleStatus,
    bool IsCuentaCorriente,
    decimal GeneralDiscountPercent,
    decimal OriginalTotal,
    decimal TotalAmount,
    decimal? VatRate,
    decimal? VatAmount,
    decimal CardSurchargeTotal,
    decimal? ManualOverridePrice,
    Guid? OverriddenByUserId,
    DateTime? OverriddenAt,
    decimal MonetaryPaidAmount,
    decimal TradeInAmount,
    decimal SettledAmount,
    decimal PendingAmount,
    decimal CcPaidTotal,
    decimal CcPendingAmount,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt,
    IReadOnlyList<GetSaleByIdDetailResponse> Details,
    IReadOnlyList<GetSaleByIdPaymentResponse> Payments,
    IReadOnlyList<GetSaleByIdCcPaymentResponse> CcPayments,
    IReadOnlyList<GetSaleByIdTradeInResponse> TradeIns);

public sealed record GetSaleByIdTradeInResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);

public sealed record GetSaleByIdDetailResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TotalAmount);

public sealed record GetSaleByIdPaymentResponse(
    int IdPaymentMethod,
    string PaymentMethod,
    decimal Amount,
    string? Reference,
    int? TransferBankId = null);

public sealed record GetSaleByIdCcPaymentResponse(
    Guid Id,
    int IdPaymentMethod,
    string PaymentMethodName,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int Status,
    string StatusName,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    Guid? GroupId);

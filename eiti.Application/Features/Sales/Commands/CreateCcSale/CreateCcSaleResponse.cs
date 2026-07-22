namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed record CreateCcSaleResponse(
    Guid Id,
    string? Code,
    Guid BranchId,
    Guid CustomerId,
    string? CustomerFullName,
    int IdSaleStatus,
    string SaleStatus,
    decimal GeneralDiscountPercent,
    decimal OriginalTotal,
    decimal TotalAmount,
    decimal? VatRate,
    decimal? VatAmount,
    decimal? ManualOverridePrice,
    bool IsCuentaCorriente,
    DateTime CreatedAt,
    decimal CreditApplied,
    decimal RemainingCustomerCredit,
    IReadOnlyList<CreateCcSaleDetailItemResponse> Details,
    decimal TradeInAmount,
    decimal CcPendingAmount,
    IReadOnlyList<CreateCcSaleTradeInItemResponse> TradeIns);

public sealed record CreateCcSaleDetailItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TotalAmount);

public sealed record CreateCcSaleTradeInItemResponse(
    Guid ProductId,
    string ProductName,
    string ProductBrand,
    int Quantity,
    decimal Amount);

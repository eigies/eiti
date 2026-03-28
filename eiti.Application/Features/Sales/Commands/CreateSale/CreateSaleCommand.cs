using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateSale;

public sealed record CreateSaleCommand(
    Guid BranchId,
    Guid? CustomerId,
    int IdSaleStatus,
    bool HasDelivery,
    Guid? CashDrawerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details,
    IReadOnlyList<CreateSalePaymentItemRequest> Payments,
    IReadOnlyList<CreateSaleTradeInItemRequest> TradeIns,
    decimal? NoDeliverySurchargeTotal = null,
    SaleSourceChannel? SourceChannel = null,
    string? DeliveryAddress = null,
    decimal GeneralDiscountPercent = 0
) : IRequest<Result<CreateSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreate];
}

public sealed record CreateSaleDetailItemRequest(
    Guid ProductId,
    int Quantity,
    decimal? UnitPrice = null,
    decimal DiscountPercent = 0);

public sealed record CreateSalePaymentChequeData(
    string Numero,
    int BankId,
    string Titular,
    string CuitDni,
    decimal Monto,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string? Notas);

public sealed record CreateSalePaymentItemRequest(
    int IdPaymentMethod,
    decimal Amount,
    string? Reference,
    int? CardBankId = null,
    int? CardCuotas = null,
    CreateSalePaymentChequeData? Cheque = null);

public sealed record CreateSaleTradeInItemRequest(
    Guid ProductId,
    int Quantity,
    decimal Amount);

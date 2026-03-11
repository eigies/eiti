namespace eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;

public sealed record SendSaleWhatsAppResponse(
    Guid SaleId,
    string ToPhone,
    string Message,
    string LaunchUrl,
    bool RequiresUserAction);

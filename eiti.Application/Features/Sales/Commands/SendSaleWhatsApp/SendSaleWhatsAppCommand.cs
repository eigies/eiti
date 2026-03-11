using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.SendSaleWhatsApp;

public sealed record SendSaleWhatsAppCommand(
    Guid Id
) : IRequest<Result<SendSaleWhatsAppResponse>>;

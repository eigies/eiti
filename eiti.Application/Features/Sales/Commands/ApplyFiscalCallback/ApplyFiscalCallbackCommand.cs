using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.ApplyFiscalCallback;

/// <summary>
/// Resolución diferida que manda el servicio fiscal. NO lleva IRequirePermissions: no la dispara
/// un usuario, la dispara el servicio y se autentica con la firma HMAC del request.
/// </summary>
public sealed record ApplyFiscalCallbackCommand(
    Guid DocumentId,
    string? SaleId,
    string? Status,
    string? AuthorizationCode,
    long? Number,
    string? Qr,
    string? RejectionReason) : IRequest<Result>;

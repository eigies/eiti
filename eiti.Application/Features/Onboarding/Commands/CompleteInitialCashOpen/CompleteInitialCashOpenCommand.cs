using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using MediatR;

namespace eiti.Application.Features.Onboarding.Commands.CompleteInitialCashOpen;

public sealed record CompleteInitialCashOpenCommand(
    Guid CashDrawerId,
    decimal OpeningAmount,
    string? Notes
) : IRequest<Result<CashSessionResponse>>;

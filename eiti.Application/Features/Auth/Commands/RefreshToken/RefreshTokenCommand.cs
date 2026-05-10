using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<RefreshTokenResponse>>;

public sealed record RefreshTokenResponse(string Token, string RefreshToken);

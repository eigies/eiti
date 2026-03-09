using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Username,
    string Email,
    string Password,
    string CompanyName
) : IRequest<Result<RegisterResponse>>;

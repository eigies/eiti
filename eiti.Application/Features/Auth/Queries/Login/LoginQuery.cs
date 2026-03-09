using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.Auth.Queries.Login;

public sealed record LoginQuery(
    string UsernameOrEmail,
    string Password
) : IRequest<Result<LoginResponse>>;

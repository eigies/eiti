namespace eiti.Application.Features.Auth.Commands.Register;

public sealed record RegisterResponse(
    Guid UserId,
    string Username,
    string Email,
    string Token,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

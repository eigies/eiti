namespace eiti.Application.Features.Auth.Commands.Register;

public sealed record RegisterResponse(
    Guid UserId,
    string Username,
    string Email,
    string Token,
    IReadOnlyList<string> Roles,
    Guid? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Permissions);

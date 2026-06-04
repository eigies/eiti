namespace eiti.Application.Features.Auth.Commands.Register;

public sealed record RegisterResponse(
    Guid UserId,
    string Username,
    string Email,
    string Token,
    string RefreshToken,
    Guid? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Permissions,
    bool CanViewAllBranches);

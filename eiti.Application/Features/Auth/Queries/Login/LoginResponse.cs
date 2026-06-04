namespace eiti.Application.Features.Auth.Queries.Login;

public sealed record LoginResponse(
    Guid UserId,
    string Username,
    string Email,
    string Token,
    string RefreshToken,
    Guid? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Permissions,
    Guid? AssignedCashDrawerId,
    bool CanViewAllBranches);

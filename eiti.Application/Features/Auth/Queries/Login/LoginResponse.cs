namespace eiti.Application.Features.Auth.Queries.Login;

public sealed record LoginResponse(
    Guid UserId,
    string Username,
    string Email,
    string Token,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? AssignedCashDrawerId);

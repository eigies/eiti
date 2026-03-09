namespace eiti.Application.Common.Authorization;

public sealed record RoleDefinition(
    string Code,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);

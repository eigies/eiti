using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class UserRoleAssignment
{
    public UserId UserId { get; private set; } = UserId.Empty;
    public string RoleCode { get; private set; } = string.Empty;
    public DateTime AssignedAt { get; private set; }

    private UserRoleAssignment()
    {
    }

    private UserRoleAssignment(UserId userId, string roleCode)
    {
        UserId = userId;
        RoleCode = NormalizeRoleCode(roleCode);
        AssignedAt = DateTime.UtcNow;
    }

    public static UserRoleAssignment Create(UserId userId, string roleCode) => new(userId, roleCode);

    private static string NormalizeRoleCode(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            throw new ArgumentException("Role code cannot be empty.", nameof(roleCode));
        }

        return roleCode.Trim().ToLowerInvariant();
    }
}

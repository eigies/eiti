using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class UserRoleAudit : Entity<UserRoleAuditId>
{
    public CompanyId CompanyId { get; private set; } = null!;
    public UserId TargetUserId { get; private set; } = null!;
    public UserId? ChangedByUserId { get; private set; }
    public AccessProfileId? PreviousAccessProfileId { get; private set; }
    public string? PreviousAccessProfileName { get; private set; }
    public string PreviousPermissionCodesCsv { get; private set; } = string.Empty;
    public AccessProfileId? NewAccessProfileId { get; private set; }
    public string? NewAccessProfileName { get; private set; }
    public string NewPermissionCodesCsv { get; private set; } = string.Empty;
    public string PreviousRolesCsv { get; private set; } = string.Empty;
    public string NewRolesCsv { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    private UserRoleAudit()
    {
    }

    private UserRoleAudit(
        UserRoleAuditId id,
        CompanyId companyId,
        UserId targetUserId,
        UserId? changedByUserId,
        AccessProfileId? previousAccessProfileId,
        string? previousAccessProfileName,
        string previousPermissionCodesCsv,
        AccessProfileId? newAccessProfileId,
        string? newAccessProfileName,
        string newPermissionCodesCsv,
        string previousRolesCsv,
        string newRolesCsv,
        DateTime changedAt)
        : base(id)
    {
        CompanyId = companyId;
        TargetUserId = targetUserId;
        ChangedByUserId = changedByUserId;
        PreviousAccessProfileId = previousAccessProfileId;
        PreviousAccessProfileName = previousAccessProfileName;
        PreviousPermissionCodesCsv = previousPermissionCodesCsv;
        NewAccessProfileId = newAccessProfileId;
        NewAccessProfileName = newAccessProfileName;
        NewPermissionCodesCsv = newPermissionCodesCsv;
        PreviousRolesCsv = previousRolesCsv;
        NewRolesCsv = newRolesCsv;
        ChangedAt = changedAt;
    }

    public static UserRoleAudit Create(
        CompanyId companyId,
        UserId targetUserId,
        UserId? changedByUserId,
        AccessProfile? previousProfile,
        AccessProfile? newProfile,
        IEnumerable<string>? previousRoles = null,
        IEnumerable<string>? newRoles = null)
    {
        var previousProfilePermissions = Normalize(previousProfile?.Permissions.Select(permission => permission.PermissionCode) ?? []);
        var newProfilePermissions = Normalize(newProfile?.Permissions.Select(permission => permission.PermissionCode) ?? []);
        var previous = Normalize(previousRoles ?? []);
        var current = Normalize(newRoles ?? []);

        return new UserRoleAudit(
            UserRoleAuditId.New(),
            companyId,
            targetUserId,
            changedByUserId,
            previousProfile?.Id,
            previousProfile?.Name,
            string.Join(',', previousProfilePermissions),
            newProfile?.Id,
            newProfile?.Name,
            string.Join(',', newProfilePermissions),
            string.Join(',', previous),
            string.Join(',', current),
            DateTime.UtcNow);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> roles) =>
        roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();
}

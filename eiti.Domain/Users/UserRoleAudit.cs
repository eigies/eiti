using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class UserRoleAudit : Entity<UserRoleAuditId>
{
    public CompanyId CompanyId { get; private set; }
    public UserId TargetUserId { get; private set; }
    public UserId? ChangedByUserId { get; private set; }
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
        string previousRolesCsv,
        string newRolesCsv,
        DateTime changedAt)
        : base(id)
    {
        CompanyId = companyId;
        TargetUserId = targetUserId;
        ChangedByUserId = changedByUserId;
        PreviousRolesCsv = previousRolesCsv;
        NewRolesCsv = newRolesCsv;
        ChangedAt = changedAt;
    }

    public static UserRoleAudit Create(
        CompanyId companyId,
        UserId targetUserId,
        UserId? changedByUserId,
        IEnumerable<string> previousRoles,
        IEnumerable<string> newRoles)
    {
        var previous = Normalize(previousRoles);
        var current = Normalize(newRoles);

        return new UserRoleAudit(
            UserRoleAuditId.New(),
            companyId,
            targetUserId,
            changedByUserId,
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

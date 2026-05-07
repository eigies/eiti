using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class AccessProfile : AggregateRoot<AccessProfileId>
{
    private readonly List<AccessProfilePermission> _permissions = [];

    public CompanyId CompanyId { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? SystemKey { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<AccessProfilePermission> Permissions => _permissions;

    private AccessProfile()
    {
    }

    private AccessProfile(
        AccessProfileId id,
        CompanyId companyId,
        string name,
        string? description,
        string? systemKey,
        bool isSystem,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        Name = name;
        Description = description;
        SystemKey = string.IsNullOrWhiteSpace(systemKey) ? null : systemKey.Trim().ToLowerInvariant();
        IsSystem = isSystem;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static AccessProfile Create(
        CompanyId companyId,
        string name,
        string? description,
        IEnumerable<string> permissionCodes,
        bool isSystem = false,
        string? systemKey = null)
    {
        var profile = new AccessProfile(
            AccessProfileId.New(),
            companyId,
            NormalizeName(name),
            NormalizeDescription(description),
            systemKey,
            isSystem,
            DateTime.UtcNow);

        profile.ReplacePermissions(permissionCodes);
        return profile;
    }

    public void UpdateDefinition(string name, string? description, IEnumerable<string> permissionCodes)
    {
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        ReplacePermissions(permissionCodes);
    }

    public void ReplacePermissions(IEnumerable<string> permissionCodes)
    {
        var normalizedPermissions = NormalizePermissions(permissionCodes);
        if (normalizedPermissions.Count == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissionCodes));
        }

        _permissions.Clear();
        foreach (var permissionCode in normalizedPermissions)
        {
            _permissions.Add(AccessProfilePermission.Create(Id, permissionCode));
        }

        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeName(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Profile name is required.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static IReadOnlyList<string> NormalizePermissions(IEnumerable<string> permissionCodes) =>
        permissionCodes
            .Where(permissionCode => !string.IsNullOrWhiteSpace(permissionCode))
            .Select(permissionCode => permissionCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permissionCode => permissionCode)
            .ToArray();
}

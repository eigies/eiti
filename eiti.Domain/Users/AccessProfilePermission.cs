namespace eiti.Domain.Users;

public sealed class AccessProfilePermission
{
    public AccessProfileId AccessProfileId { get; private set; } = null!;
    public string PermissionCode { get; private set; } = string.Empty;
    public DateTime AssignedAt { get; private set; }

    private AccessProfilePermission()
    {
    }

    private AccessProfilePermission(AccessProfileId accessProfileId, string permissionCode)
    {
        AccessProfileId = accessProfileId;
        PermissionCode = permissionCode;
        AssignedAt = DateTime.UtcNow;
    }

    public static AccessProfilePermission Create(AccessProfileId accessProfileId, string permissionCode) =>
        new(accessProfileId, permissionCode);
}

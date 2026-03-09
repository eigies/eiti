namespace eiti.Domain.Users;

public sealed record UserRoleAuditId(Guid Value)
{
    public static UserRoleAuditId New() => new(Guid.NewGuid());
}

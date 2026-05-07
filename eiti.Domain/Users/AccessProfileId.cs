namespace eiti.Domain.Users;

public sealed record AccessProfileId(Guid Value)
{
    public static AccessProfileId New() => new(Guid.NewGuid());
}

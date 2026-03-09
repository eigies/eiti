namespace eiti.Domain.Fleet;

public sealed record FleetLogId(Guid Value)
{
    public static FleetLogId New() => new(Guid.NewGuid());
}

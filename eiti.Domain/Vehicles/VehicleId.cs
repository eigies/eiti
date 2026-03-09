namespace eiti.Domain.Vehicles;

public sealed record VehicleId(Guid Value)
{
    public static VehicleId New() => new(Guid.NewGuid());
}

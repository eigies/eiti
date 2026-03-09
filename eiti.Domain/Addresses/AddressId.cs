namespace eiti.Domain.Addresses;

public sealed record AddressId(Guid Value)
{
    public static AddressId New() => new(Guid.NewGuid());

    public static AddressId Empty => new(Guid.Empty);
}

namespace eiti.Domain.Transport;

public sealed record SaleTransportAssignmentId(Guid Value)
{
    public static SaleTransportAssignmentId New() => new(Guid.NewGuid());
}

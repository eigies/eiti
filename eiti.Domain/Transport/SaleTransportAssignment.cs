using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;
using eiti.Domain.Sales;
using eiti.Domain.Users;
using eiti.Domain.Vehicles;

namespace eiti.Domain.Transport;

public sealed class SaleTransportAssignment : AggregateRoot<SaleTransportAssignmentId>
{
    public SaleId SaleId { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public EmployeeId DriverEmployeeId { get; private set; }
    public VehicleId VehicleId { get; private set; }
    public SaleTransportStatus Status { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public string? Notes { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SaleTransportAssignment()
    {
    }

    private SaleTransportAssignment(
        SaleTransportAssignmentId id,
        SaleId saleId,
        CompanyId companyId,
        EmployeeId driverEmployeeId,
        VehicleId vehicleId,
        string? notes,
        UserId createdByUserId,
        DateTime assignedAt)
        : base(id)
    {
        SaleId = saleId;
        CompanyId = companyId;
        DriverEmployeeId = driverEmployeeId;
        VehicleId = vehicleId;
        Status = SaleTransportStatus.Assigned;
        Notes = notes;
        CreatedByUserId = createdByUserId;
        AssignedAt = assignedAt;
    }

    public static SaleTransportAssignment Create(
        SaleId saleId,
        CompanyId companyId,
        EmployeeId driverEmployeeId,
        VehicleId vehicleId,
        string? notes,
        UserId createdByUserId)
    {
        return new SaleTransportAssignment(
            SaleTransportAssignmentId.New(),
            saleId,
            companyId,
            driverEmployeeId,
            vehicleId,
            NormalizeOptional(notes, 500, nameof(notes)),
            createdByUserId,
            DateTime.UtcNow);
    }

    public void UpdateAssignment(EmployeeId driverEmployeeId, VehicleId vehicleId, string? notes)
    {
        if (Status != SaleTransportStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned transports can be updated.");
        }

        DriverEmployeeId = driverEmployeeId;
        VehicleId = vehicleId;
        Notes = NormalizeOptional(notes, 500, nameof(notes));
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkInTransit()
    {
        if (Status != SaleTransportStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned transports can move to in transit.");
        }

        Status = SaleTransportStatus.InTransit;
        DispatchedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        if (Status != SaleTransportStatus.InTransit)
        {
            throw new InvalidOperationException("Only in-transit transports can be delivered.");
        }

        Status = SaleTransportStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == SaleTransportStatus.Delivered)
        {
            throw new InvalidOperationException("Delivered transports cannot be cancelled.");
        }

        Status = SaleTransportStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
        }

        return normalized;
    }
}

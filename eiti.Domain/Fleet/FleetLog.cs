using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;
using eiti.Domain.Users;
using eiti.Domain.Vehicles;

namespace eiti.Domain.Fleet;

public sealed class FleetLog : AggregateRoot<FleetLogId>
{
    public CompanyId CompanyId { get; private set; }
    public VehicleId VehicleId { get; private set; }
    public EmployeeId? PerformedByEmployeeId { get; private set; }
    public FleetLogType Type { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public decimal? Odometer { get; private set; }
    public decimal? FuelLiters { get; private set; }
    public decimal? FuelCost { get; private set; }
    public string? MaintenanceType { get; private set; }
    public string Description { get; private set; }
    public string? Notes { get; private set; }
    public UserId CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private FleetLog()
    {
    }

    private FleetLog(
        FleetLogId id,
        CompanyId companyId,
        VehicleId vehicleId,
        EmployeeId? performedByEmployeeId,
        FleetLogType type,
        DateTime occurredAt,
        decimal? odometer,
        decimal? fuelLiters,
        decimal? fuelCost,
        string? maintenanceType,
        string description,
        string? notes,
        UserId createdByUserId,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        VehicleId = vehicleId;
        PerformedByEmployeeId = performedByEmployeeId;
        Type = type;
        OccurredAt = occurredAt;
        Odometer = odometer;
        FuelLiters = fuelLiters;
        FuelCost = fuelCost;
        MaintenanceType = maintenanceType;
        Description = description;
        Notes = notes;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public static FleetLog Create(
        CompanyId companyId,
        VehicleId vehicleId,
        EmployeeId? performedByEmployeeId,
        FleetLogType type,
        DateTime occurredAt,
        decimal? odometer,
        decimal? fuelLiters,
        decimal? fuelCost,
        string? maintenanceType,
        string description,
        string? notes,
        UserId createdByUserId)
    {
        if (type == FleetLogType.FuelLoad && (!fuelLiters.HasValue || fuelLiters.Value <= 0))
        {
            throw new ArgumentException("Fuel load entries require a positive fuel amount.", nameof(fuelLiters));
        }

        return new FleetLog(
            FleetLogId.New(),
            companyId,
            vehicleId,
            performedByEmployeeId,
            type,
            occurredAt,
            odometer,
            fuelLiters,
            fuelCost,
            NormalizeOptional(maintenanceType, 80, nameof(maintenanceType)),
            NormalizeRequired(description, 240, nameof(description)),
            NormalizeOptional(notes, 500, nameof(notes)),
            createdByUserId,
            DateTime.UtcNow);
    }

    private static string NormalizeRequired(string value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} cannot be empty.", field);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{field} cannot exceed {maxLength} characters.", field);
        }

        return normalized;
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

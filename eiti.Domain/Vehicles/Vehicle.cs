using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Vehicles;

public sealed class Vehicle : AggregateRoot<VehicleId>
{
    public CompanyId CompanyId { get; private set; }
    public BranchId? BranchId { get; private set; }
    public EmployeeId? AssignedDriverEmployeeId { get; private set; }
    public string Plate { get; private set; }
    public string Model { get; private set; }
    public string? Brand { get; private set; }
    public int? Year { get; private set; }
    public FuelType FuelType { get; private set; }
    public decimal? CurrentOdometer { get; private set; }
    public DateTime? LastFuelLoadedAt { get; private set; }
    public DateTime? LastMaintenanceAt { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Vehicle()
    {
    }

    private Vehicle(
        VehicleId id,
        CompanyId companyId,
        BranchId? branchId,
        EmployeeId? assignedDriverEmployeeId,
        string plate,
        string model,
        string? brand,
        int? year,
        FuelType fuelType,
        decimal? currentOdometer,
        string? notes,
        bool isActive,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        AssignedDriverEmployeeId = assignedDriverEmployeeId;
        Plate = plate;
        Model = model;
        Brand = brand;
        Year = year;
        FuelType = fuelType;
        CurrentOdometer = currentOdometer;
        Notes = notes;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public static Vehicle Create(
        CompanyId companyId,
        BranchId? branchId,
        EmployeeId? assignedDriverEmployeeId,
        string plate,
        string model,
        string? brand,
        int? year,
        FuelType fuelType,
        decimal? currentOdometer,
        string? notes)
    {
        return new Vehicle(
            VehicleId.New(),
            companyId,
            branchId,
            assignedDriverEmployeeId,
            NormalizeRequired(plate, 20, nameof(plate)).ToUpperInvariant(),
            NormalizeRequired(model, 120, nameof(model)),
            NormalizeOptional(brand, 120, nameof(brand)),
            year,
            fuelType,
            currentOdometer,
            NormalizeOptional(notes, 500, nameof(notes)),
            true,
            DateTime.UtcNow);
    }

    public void Update(
        BranchId? branchId,
        string plate,
        string model,
        string? brand,
        int? year,
        FuelType fuelType,
        decimal? currentOdometer,
        string? notes)
    {
        BranchId = branchId;
        Plate = NormalizeRequired(plate, 20, nameof(plate)).ToUpperInvariant();
        Model = NormalizeRequired(model, 120, nameof(model));
        Brand = NormalizeOptional(brand, 120, nameof(brand));
        Year = year;
        FuelType = fuelType;
        CurrentOdometer = currentOdometer;
        Notes = NormalizeOptional(notes, 500, nameof(notes));
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignDriver(EmployeeId employeeId)
    {
        AssignedDriverEmployeeId = employeeId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnassignDriver()
    {
        AssignedDriverEmployeeId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegisterFuelLoad(DateTime occurredAt, decimal? odometer)
    {
        ApplyOdometer(odometer);
        LastFuelLoadedAt = occurredAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RegisterMaintenance(DateTime occurredAt, decimal? odometer)
    {
        ApplyOdometer(odometer);
        LastMaintenanceAt = occurredAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyOdometer(decimal? odometer)
    {
        if (!odometer.HasValue)
        {
            return;
        }

        if (CurrentOdometer.HasValue && odometer.Value < CurrentOdometer.Value)
        {
            throw new InvalidOperationException("Odometer cannot move backwards.");
        }

        CurrentOdometer = odometer.Value;
        UpdatedAt = DateTime.UtcNow;
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

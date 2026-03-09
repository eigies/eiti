using eiti.Domain.Companies;

namespace eiti.Domain.Employees;

public sealed class DriverProfile
{
    public EmployeeId EmployeeId { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public string LicenseNumber { get; private set; }
    public string? LicenseCategory { get; private set; }
    public DateTime? LicenseExpiresAt { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private DriverProfile()
    {
    }

    private DriverProfile(
        EmployeeId employeeId,
        CompanyId companyId,
        string licenseNumber,
        string? licenseCategory,
        DateTime? licenseExpiresAt,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? notes,
        DateTime createdAt)
    {
        EmployeeId = employeeId;
        CompanyId = companyId;
        LicenseNumber = licenseNumber;
        LicenseCategory = licenseCategory;
        LicenseExpiresAt = licenseExpiresAt;
        EmergencyContactName = emergencyContactName;
        EmergencyContactPhone = emergencyContactPhone;
        Notes = notes;
        CreatedAt = createdAt;
    }

    public static DriverProfile Create(
        EmployeeId employeeId,
        CompanyId companyId,
        string licenseNumber,
        string? licenseCategory,
        DateTime? licenseExpiresAt,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? notes)
    {
        return new DriverProfile(
            employeeId,
            companyId,
            NormalizeRequired(licenseNumber, 60, nameof(licenseNumber)),
            NormalizeOptional(licenseCategory, 40, nameof(licenseCategory)),
            licenseExpiresAt,
            NormalizeOptional(emergencyContactName, 120, nameof(emergencyContactName)),
            NormalizeOptional(emergencyContactPhone, 40, nameof(emergencyContactPhone)),
            NormalizeOptional(notes, 500, nameof(notes)),
            DateTime.UtcNow);
    }

    public void Update(
        string licenseNumber,
        string? licenseCategory,
        DateTime? licenseExpiresAt,
        string? emergencyContactName,
        string? emergencyContactPhone,
        string? notes)
    {
        LicenseNumber = NormalizeRequired(licenseNumber, 60, nameof(licenseNumber));
        LicenseCategory = NormalizeOptional(licenseCategory, 40, nameof(licenseCategory));
        LicenseExpiresAt = licenseExpiresAt;
        EmergencyContactName = NormalizeOptional(emergencyContactName, 120, nameof(emergencyContactName));
        EmergencyContactPhone = NormalizeOptional(emergencyContactPhone, 40, nameof(emergencyContactPhone));
        Notes = NormalizeOptional(notes, 500, nameof(notes));
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsExpired(DateTime nowUtc) => LicenseExpiresAt.HasValue && LicenseExpiresAt.Value < nowUtc;

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

using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Primitives;

namespace eiti.Domain.Employees;

public sealed class Employee : AggregateRoot<EmployeeId>
{
    public CompanyId CompanyId { get; private set; }
    public BranchId? BranchId { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public EmployeeRole EmployeeRole { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string FullName => $"{FirstName} {LastName}".Trim();

    private Employee()
    {
    }

    private Employee(
        EmployeeId id,
        CompanyId companyId,
        BranchId? branchId,
        string firstName,
        string lastName,
        string? documentNumber,
        string? phone,
        string? email,
        EmployeeRole employeeRole,
        bool isActive,
        DateTime createdAt)
        : base(id)
    {
        CompanyId = companyId;
        BranchId = branchId;
        FirstName = firstName;
        LastName = lastName;
        DocumentNumber = documentNumber;
        Phone = phone;
        Email = email;
        EmployeeRole = employeeRole;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public static Employee Create(
        CompanyId companyId,
        BranchId? branchId,
        string firstName,
        string lastName,
        string? documentNumber,
        string? phone,
        string? email,
        EmployeeRole employeeRole)
    {
        return new Employee(
            EmployeeId.New(),
            companyId,
            branchId,
            NormalizeRequired(firstName, 80, nameof(firstName)),
            NormalizeRequired(lastName, 80, nameof(lastName)),
            NormalizeOptional(documentNumber, 40, nameof(documentNumber)),
            NormalizeOptional(phone, 40, nameof(phone)),
            NormalizeOptional(email, 160, nameof(email)),
            employeeRole,
            true,
            DateTime.UtcNow);
    }

    public void Update(
        BranchId? branchId,
        string firstName,
        string lastName,
        string? documentNumber,
        string? phone,
        string? email,
        EmployeeRole employeeRole)
    {
        BranchId = branchId;
        FirstName = NormalizeRequired(firstName, 80, nameof(firstName));
        LastName = NormalizeRequired(lastName, 80, nameof(lastName));
        DocumentNumber = NormalizeOptional(documentNumber, 40, nameof(documentNumber));
        Phone = NormalizeOptional(phone, 40, nameof(phone));
        Email = NormalizeOptional(email, 160, nameof(email));
        EmployeeRole = employeeRole;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
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

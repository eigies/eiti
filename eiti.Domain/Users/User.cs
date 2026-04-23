using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    public Username Username { get; private set; }
    public Email Email { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public CompanyId CompanyId { get; private set; }
    public EmployeeId? EmployeeId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    private readonly List<UserRoleAssignment> _roles = [];
    public IReadOnlyCollection<UserRoleAssignment> Roles => _roles;

    private User()
    {
    }

    private User(
        UserId id,
        Username username,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        EmployeeId? employeeId,
        DateTime createdAt)
        : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CompanyId = companyId;
        EmployeeId = employeeId;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static User Create(
        Username username,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        IEnumerable<string> roleCodes,
        EmployeeId? employeeId = null)
    {
        var user = new User(
            UserId.New(),
            username,
            email,
            passwordHash,
            companyId,
            employeeId,
            DateTime.UtcNow);

        user.AssignRoles(roleCodes);
        return user;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void AssignRoles(IEnumerable<string> roleCodes)
    {
        _roles.Clear();

        foreach (var roleCode in roleCodes
                     .Where(code => !string.IsNullOrWhiteSpace(code))
                     .Select(code => code.Trim().ToLowerInvariant())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _roles.Add(UserRoleAssignment.Create(Id, roleCode));
        }

        if (_roles.Count == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roleCodes));
        }
    }

    public void LinkEmployee(EmployeeId? employeeId)
    {
        EmployeeId = employeeId;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void ChangePassword(PasswordHash newHash)
    {
        PasswordHash = newHash;
    }
}

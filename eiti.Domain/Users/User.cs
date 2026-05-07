using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    public Username Username { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId? EmployeeId { get; private set; }
    public AccessProfileId AccessProfileId { get; private set; } = null!;
    public AccessProfile AccessProfile { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    private User()
    {
    }

    private User(
        UserId id,
        Username username,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        AccessProfileId accessProfileId,
        EmployeeId? employeeId,
        DateTime createdAt)
        : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CompanyId = companyId;
        AccessProfileId = accessProfileId;
        EmployeeId = employeeId;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public static User Create(
        Username username,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        AccessProfile accessProfile,
        EmployeeId? employeeId = null)
    {
        var user = new User(
            UserId.New(),
            username,
            email,
            passwordHash,
            companyId,
            accessProfile.Id,
            employeeId,
            DateTime.UtcNow);

        user.AccessProfile = accessProfile;
        return user;
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void AssignProfile(AccessProfile accessProfile)
    {
        AccessProfileId = accessProfile.Id;
        AccessProfile = accessProfile;
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

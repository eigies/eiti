using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Primitives;

namespace eiti.Domain.Users;

public sealed class User : AggregateRoot<UserId>
{
    public Username Username { get; private set; } = null!;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public CompanyId CompanyId { get; private set; } = null!;
    public EmployeeId? EmployeeId { get; private set; }
    public AccessProfileId AccessProfileId { get; private set; } = null!;
    public AccessProfile AccessProfile { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    private readonly List<UserBranchAccess> _branchAccesses = [];
    public IReadOnlyCollection<UserBranchAccess> BranchAccesses => _branchAccesses.AsReadOnly();
    public IReadOnlyCollection<Guid> BranchAccessIds => _branchAccesses.Select(b => b.BranchId.Value).ToList().AsReadOnly();

    private User()
    {
    }

    private User(
        UserId id,
        Username username,
        string firstName,
        string lastName,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        AccessProfileId accessProfileId,
        EmployeeId? employeeId,
        DateTime createdAt)
        : base(id)
    {
        Username = username;
        FirstName = NormalizeRequired(firstName, 80, nameof(firstName));
        LastName = NormalizeRequired(lastName, 80, nameof(lastName));
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
        string firstName,
        string lastName,
        Email email,
        PasswordHash passwordHash,
        CompanyId companyId,
        AccessProfile accessProfile,
        EmployeeId? employeeId = null)
    {
        var user = new User(
            UserId.New(),
            username,
            firstName,
            lastName,
            email,
            passwordHash,
            companyId,
            accessProfile.Id,
            employeeId,
            DateTime.UtcNow);

        user.AccessProfile = accessProfile;
        return user;
    }

    public void UpdateName(string firstName, string lastName)
    {
        FirstName = NormalizeRequired(firstName, 80, nameof(firstName));
        LastName = NormalizeRequired(lastName, 80, nameof(lastName));
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

    public void SetBranchAccess(IEnumerable<BranchId> branchIds)
    {
        _branchAccesses.Clear();
        foreach (var branchId in branchIds.Distinct())
        {
            _branchAccesses.Add(UserBranchAccess.Create(Id, branchId));
        }
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

    public void SetRefreshToken(string token, DateTime expiresAt)
    {
        RefreshToken = token;
        RefreshTokenExpiresAt = expiresAt;
    }

    public bool IsRefreshTokenValid(string token)
        => RefreshToken == token && RefreshTokenExpiresAt > DateTime.UtcNow;
}

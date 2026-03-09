using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Users;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    Guid? EmployeeId,
    string? EmployeeName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record UserRoleAuditResponse(
    Guid Id,
    Guid TargetUserId,
    string TargetUsername,
    Guid? ChangedByUserId,
    string? ChangedByUsername,
    IReadOnlyList<string> PreviousRoles,
    IReadOnlyList<string> NewRoles,
    DateTime ChangedAt);

public sealed record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    IReadOnlyList<string> RoleCodes,
    Guid? EmployeeId) : IRequest<Result<UserResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record GetUserQuery(Guid Id) : IRequest<Result<UserResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record GetMyProfileQuery() : IRequest<Result<UserResponse>>;

public sealed record ListUsersQuery() : IRequest<Result<IReadOnlyList<UserResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record UpdateUserRolesCommand(
    Guid Id,
    IReadOnlyList<string> RoleCodes,
    Guid? EmployeeId) : IRequest<Result<UserResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record SetUserActiveStatusCommand(
    Guid Id,
    bool IsActive) : IRequest<Result<UserResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record ListUserRoleAuditsQuery(
    Guid? UserId,
    int Take = 50) : IRequest<Result<IReadOnlyList<UserRoleAuditResponse>>>;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UserResponse>.Failure(Error.Unauthorized("Users.Create.Unauthorized", "The current user is not authenticated."));
        }

        var normalizedRoles = NormalizeRoles(request.RoleCodes);
        if (normalizedRoles.Count == 0)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Create.RoleRequired", "Select at least one role."));
        }

        Username username;
        Email email;

        try
        {
            username = Username.Create(request.Username);
            email = Email.Create(request.Email);
        }
        catch (ArgumentException ex)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Create.InvalidInput", ex.Message));
        }

        if (await _userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            return Result<UserResponse>.Failure(Error.Conflict("Users.Create.UsernameExists", "An user with the same username already exists."));
        }

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            return Result<UserResponse>.Failure(Error.Conflict("Users.Create.EmailExists", "An user with the same email already exists."));
        }

        Employee? employee = await ResolveEmployeeAsync(request.EmployeeId, cancellationToken);
        if (request.EmployeeId.HasValue && employee is null)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Create.EmployeeNotFound", "The selected employee was not found."));
        }

        var passwordHash = PasswordHash.Create(_passwordHasher.HashPassword(request.Password));
        var user = User.Create(username, email, passwordHash, _currentUserService.CompanyId, normalizedRoles, employee?.Id);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRoleAuditRepository.AddAsync(
            UserRoleAudit.Create(
                _currentUserService.CompanyId,
                user.Id,
                _currentUserService.UserId,
                [],
                normalizedRoles),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(UserMappings.Map(user, employee));
    }

    private IReadOnlyList<string> NormalizeRoles(IEnumerable<string> roleCodes)
    {
        var normalized = roleCodes
            .Where(roleCode => !string.IsNullOrWhiteSpace(roleCode))
            .Select(roleCode => roleCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Any(roleCode => !RoleCatalog.IsValid(roleCode)))
        {
            return Array.Empty<string>();
        }

        return normalized;
    }

    private async Task<Employee?> ResolveEmployeeAsync(Guid? employeeId, CancellationToken cancellationToken)
    {
        if (!employeeId.HasValue || _currentUserService.CompanyId is null)
        {
            return null;
        }

        return await _employeeRepository.GetByIdAsync(new EmployeeId(employeeId.Value), _currentUserService.CompanyId, cancellationToken);
    }
}

public sealed class GetUserHandler : IRequestHandler<GetUserQuery, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public GetUserHandler(ICurrentUserService currentUserService, IUserRepository userRepository, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<UserResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UserResponse>.Failure(Error.Unauthorized("Users.Get.Unauthorized", "The current user is not authenticated."));
        }

        var user = await _userRepository.GetByIdAsync(new UserId(request.Id), cancellationToken);
        if (user is null || user.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Get.NotFound", "The requested user was not found."));
        }

        var employee = await ResolveEmployeeAsync(user.EmployeeId, cancellationToken);
        return Result<UserResponse>.Success(UserMappings.Map(user, employee));
    }

    private Task<Employee?> ResolveEmployeeAsync(EmployeeId? employeeId, CancellationToken cancellationToken)
    {
        if (employeeId is null || _currentUserService.CompanyId is null)
        {
            return Task.FromResult<Employee?>(null);
        }

        return _employeeRepository.GetByIdAsync(employeeId, _currentUserService.CompanyId, cancellationToken);
    }
}

public sealed class GetMyProfileHandler : IRequestHandler<GetMyProfileQuery, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public GetMyProfileHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<UserResponse>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<UserResponse>.Failure(Error.Unauthorized("Users.Me.Unauthorized", "The current user is not authenticated."));
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId, cancellationToken);
        if (user is null || user.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Me.NotFound", "The current user was not found."));
        }

        Employee? employee = null;
        if (user.EmployeeId is not null)
        {
            employee = await _employeeRepository.GetByIdAsync(user.EmployeeId, _currentUserService.CompanyId, cancellationToken);
        }

        return Result<UserResponse>.Success(UserMappings.Map(user, employee));
    }
}

public sealed class ListUsersHandler : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;

    public ListUsersHandler(ICurrentUserService currentUserService, IUserRepository userRepository, IEmployeeRepository employeeRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<Result<IReadOnlyList<UserResponse>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<UserResponse>>.Failure(Error.Unauthorized("Users.List.Unauthorized", "The current user is not authenticated."));
        }

        var users = await _userRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var employees = await _employeeRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var employeeMap = employees.ToDictionary(employee => employee.Id, employee => employee);

        var items = users
            .OrderBy(user => user.Username.Value)
            .Select(user => UserMappings.Map(user, user.EmployeeId is not null && employeeMap.TryGetValue(user.EmployeeId, out var employee) ? employee : null))
            .ToArray();

        return Result<IReadOnlyList<UserResponse>>.Success(items);
    }
}

public sealed class UpdateUserRolesHandler : IRequestHandler<UpdateUserRolesCommand, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserRolesHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UserResponse>.Failure(Error.Unauthorized("Users.Update.Unauthorized", "The current user is not authenticated."));
        }

        var user = await _userRepository.GetByIdAsync(new UserId(request.Id), cancellationToken);
        if (user is null || user.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Update.NotFound", "The requested user was not found."));
        }

        var normalizedRoles = request.RoleCodes
            .Where(roleCode => !string.IsNullOrWhiteSpace(roleCode))
            .Select(roleCode => roleCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoles.Length == 0)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Update.RoleRequired", "Select at least one role."));
        }

        if (normalizedRoles.Any(roleCode => !RoleCatalog.IsValid(roleCode)))
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Update.InvalidRole", "One or more selected roles are invalid."));
        }

        var employee = await ResolveEmployeeAsync(request.EmployeeId, cancellationToken);
        if (request.EmployeeId.HasValue && employee is null)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Update.EmployeeNotFound", "The selected employee was not found."));
        }

        var previousRoles = user.Roles
            .Select(role => role.RoleCode)
            .OrderBy(role => role)
            .ToArray();

        user.AssignRoles(normalizedRoles);
        user.LinkEmployee(employee?.Id);

        if (!previousRoles.SequenceEqual(normalizedRoles, StringComparer.OrdinalIgnoreCase))
        {
            await _userRoleAuditRepository.AddAsync(
                UserRoleAudit.Create(
                    _currentUserService.CompanyId,
                    user.Id,
                    _currentUserService.UserId,
                    previousRoles,
                    normalizedRoles),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(UserMappings.Map(user, employee));
    }

    private Task<Employee?> ResolveEmployeeAsync(Guid? employeeId, CancellationToken cancellationToken)
    {
        if (!employeeId.HasValue || _currentUserService.CompanyId is null)
        {
            return Task.FromResult<Employee?>(null);
        }

        return _employeeRepository.GetByIdAsync(new EmployeeId(employeeId.Value), _currentUserService.CompanyId, cancellationToken);
    }
}

public sealed class SetUserActiveStatusHandler : IRequestHandler<SetUserActiveStatusCommand, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetUserActiveStatusHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> Handle(SetUserActiveStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<UserResponse>.Failure(Error.Unauthorized("Users.Status.Unauthorized", "The current user is not authenticated."));
        }

        var user = await _userRepository.GetByIdAsync(new UserId(request.Id), cancellationToken);
        if (user is null || user.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Status.NotFound", "The requested user was not found."));
        }

        if (user.Id == _currentUserService.UserId && !request.IsActive)
        {
            return Result<UserResponse>.Failure(Error.Conflict("Users.Status.SelfDeactivate", "You cannot deactivate your own user."));
        }

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var employee = user.EmployeeId is null
            ? null
            : await _employeeRepository.GetByIdAsync(user.EmployeeId, _currentUserService.CompanyId, cancellationToken);

        return Result<UserResponse>.Success(UserMappings.Map(user, employee));
    }
}

public sealed class ListUserRoleAuditsHandler : IRequestHandler<ListUserRoleAuditsQuery, Result<IReadOnlyList<UserRoleAuditResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IUserRepository _userRepository;

    public ListUserRoleAuditsHandler(
        ICurrentUserService currentUserService,
        IUserRoleAuditRepository userRoleAuditRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRoleAuditRepository = userRoleAuditRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<UserRoleAuditResponse>>> Handle(ListUserRoleAuditsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null || _currentUserService.UserId is null)
        {
            return Result<IReadOnlyList<UserRoleAuditResponse>>.Failure(Error.Unauthorized("Users.Audit.Unauthorized", "The current user is not authenticated."));
        }

        var canReadAllAudits = _currentUserService.HasPermission(PermissionCodes.UsersManage);
        UserId? targetUserId = request.UserId.HasValue ? new UserId(request.UserId.Value) : null;

        if (!canReadAllAudits)
        {
            if (targetUserId is not null && targetUserId != _currentUserService.UserId)
            {
                return Result<IReadOnlyList<UserRoleAuditResponse>>.Failure(Error.Forbidden("Users.Audit.Forbidden", "You cannot access role changes for another user."));
            }

            targetUserId = _currentUserService.UserId;
        }

        var take = Math.Clamp(request.Take <= 0 ? 50 : request.Take, 1, 200);

        var audits = await _userRoleAuditRepository.ListByCompanyAsync(
            _currentUserService.CompanyId,
            targetUserId,
            take,
            cancellationToken);

        var users = await _userRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        var usernameMap = users.ToDictionary(user => user.Id, user => user.Username.Value);

        var response = audits
            .Select(audit => UserMappings.MapAudit(audit, usernameMap))
            .ToArray();

        return Result<IReadOnlyList<UserRoleAuditResponse>>.Success(response);
    }
}

internal static class UserMappings
{
    public static UserResponse Map(User user, Employee? employee)
    {
        var roles = user.Roles
            .Select(role => role.RoleCode)
            .OrderBy(role => role)
            .ToArray();
        var permissions = RoleCatalog.PermissionsFor(roles)
            .OrderBy(permission => permission)
            .ToArray();

        return new UserResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            user.IsActive,
            user.EmployeeId?.Value,
            employee?.FullName,
            roles,
            permissions,
            user.CreatedAt,
            user.LastLoginAt);
    }

    public static UserRoleAuditResponse MapAudit(UserRoleAudit audit, IReadOnlyDictionary<UserId, string> usernameMap)
    {
        usernameMap.TryGetValue(audit.TargetUserId, out var targetUsername);

        string? changedByUsername = null;
        if (audit.ChangedByUserId is not null && usernameMap.TryGetValue(audit.ChangedByUserId, out var changedByValue))
        {
            changedByUsername = changedByValue;
        }

        return new UserRoleAuditResponse(
            audit.Id.Value,
            audit.TargetUserId.Value,
            targetUsername ?? audit.TargetUserId.Value.ToString("N"),
            audit.ChangedByUserId?.Value,
            changedByUsername,
            SplitRoles(audit.PreviousRolesCsv),
            SplitRoles(audit.NewRolesCsv),
            audit.ChangedAt);
    }

    private static IReadOnlyList<string> SplitRoles(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();
}

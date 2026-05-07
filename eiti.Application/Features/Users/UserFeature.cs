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
    Guid? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record UserProfileAuditResponse(
    Guid Id,
    Guid TargetUserId,
    string TargetUsername,
    Guid? ChangedByUserId,
    string? ChangedByUsername,
    Guid? PreviousProfileId,
    string? PreviousProfileName,
    Guid? NewProfileId,
    string? NewProfileName,
    IReadOnlyList<string> PreviousPermissionCodes,
    IReadOnlyList<string> NewPermissionCodes,
    DateTime ChangedAt);

public sealed record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    Guid ProfileId,
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

public sealed record UpdateUserProfileCommand(
    Guid Id,
    Guid ProfileId,
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

public sealed record ListUserProfileAuditsQuery(
    Guid? UserId,
    int Take = 50) : IRequest<Result<IReadOnlyList<UserProfileAuditResponse>>>;

public sealed class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IAccessProfileRepository accessProfileRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _accessProfileRepository = accessProfileRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<UserResponse>.Failure(authCheck.Error);

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

        var accessProfile = await _accessProfileRepository.GetByIdAsync(new AccessProfileId(request.ProfileId), cancellationToken);
        if (accessProfile is null || accessProfile.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Create.InvalidProfile", "Select a valid access profile."));
        }

        Employee? employee = await ResolveEmployeeAsync(request.EmployeeId, cancellationToken);
        if (request.EmployeeId.HasValue && employee is null)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Create.EmployeeNotFound", "The selected employee was not found."));
        }

        var passwordHash = PasswordHash.Create(_passwordHasher.HashPassword(request.Password));
        var user = User.Create(username, email, passwordHash, _currentUserService.CompanyId, accessProfile, employee?.Id);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRoleAuditRepository.AddAsync(
            UserRoleAudit.Create(
                _currentUserService.CompanyId,
                user.Id,
                _currentUserService.UserId,
                null,
                accessProfile),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(UserMappings.Map(user, employee, accessProfile));
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
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<UserResponse>.Failure(authCheck.Error);

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
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<UserResponse>.Failure(authCheck.Error);

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
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<UserResponse>>.Failure(authCheck.Error);

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

public sealed class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IAccessProfileRepository accessProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _accessProfileRepository = accessProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<UserResponse>.Failure(authCheck.Error);

        var user = await _userRepository.GetByIdAsync(new UserId(request.Id), cancellationToken);
        if (user is null || user.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Update.NotFound", "The requested user was not found."));
        }

        var accessProfile = await _accessProfileRepository.GetByIdAsync(new AccessProfileId(request.ProfileId), cancellationToken);
        if (accessProfile is null || accessProfile.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Update.InvalidProfile", "Select a valid access profile."));
        }

        var employee = await ResolveEmployeeAsync(request.EmployeeId, cancellationToken);
        if (request.EmployeeId.HasValue && employee is null)
        {
            return Result<UserResponse>.Failure(Error.NotFound("Users.Update.EmployeeNotFound", "The selected employee was not found."));
        }

        var previousProfile = user.AccessProfile;
        var hasProfileChanged = previousProfile.Id != accessProfile.Id;

        user.AssignProfile(accessProfile);
        user.LinkEmployee(employee?.Id);

        if (hasProfileChanged)
        {
            await _userRoleAuditRepository.AddAsync(
                UserRoleAudit.Create(
                    _currentUserService.CompanyId,
                    user.Id,
                    _currentUserService.UserId,
                    previousProfile,
                    accessProfile),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Success(UserMappings.Map(user, employee, accessProfile));
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
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<UserResponse>.Failure(authCheck.Error);

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

public sealed class ListUserProfileAuditsHandler : IRequestHandler<ListUserProfileAuditsQuery, Result<IReadOnlyList<UserProfileAuditResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRoleAuditRepository _userRoleAuditRepository;
    private readonly IUserRepository _userRepository;

    public ListUserProfileAuditsHandler(
        ICurrentUserService currentUserService,
        IUserRoleAuditRepository userRoleAuditRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _userRoleAuditRepository = userRoleAuditRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<UserProfileAuditResponse>>> Handle(ListUserProfileAuditsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<UserProfileAuditResponse>>.Failure(authCheck.Error);

        var canReadAllAudits = _currentUserService.HasPermission(PermissionCodes.UsersManage);
        UserId? targetUserId = request.UserId.HasValue ? new UserId(request.UserId.Value) : null;

        if (!canReadAllAudits)
        {
            if (targetUserId is not null && targetUserId != _currentUserService.UserId)
            {
                return Result<IReadOnlyList<UserProfileAuditResponse>>.Failure(Error.Forbidden("Users.Audit.Forbidden", "You cannot access profile changes for another user."));
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

        return Result<IReadOnlyList<UserProfileAuditResponse>>.Success(response);
    }
}

internal static class UserMappings
{
    public static UserResponse Map(User user, Employee? employee, AccessProfile? accessProfile = null)
    {
        var profile = accessProfile ?? user.AccessProfile;
        var permissions = profile.Permissions
            .Select(permission => permission.PermissionCode)
            .OrderBy(permission => permission)
            .ToArray();

        return new UserResponse(
            user.Id.Value,
            user.Username.Value,
            user.Email.Value,
            user.IsActive,
            user.EmployeeId?.Value,
            employee?.FullName,
            profile.Id.Value,
            profile.Name,
            permissions,
            user.CreatedAt,
            user.LastLoginAt);
    }

    public static UserProfileAuditResponse MapAudit(UserRoleAudit audit, IReadOnlyDictionary<UserId, string> usernameMap)
    {
        usernameMap.TryGetValue(audit.TargetUserId, out var targetUsername);

        string? changedByUsername = null;
        if (audit.ChangedByUserId is not null && usernameMap.TryGetValue(audit.ChangedByUserId, out var changedByValue))
        {
            changedByUsername = changedByValue;
        }

        return new UserProfileAuditResponse(
            audit.Id.Value,
            audit.TargetUserId.Value,
            targetUsername ?? audit.TargetUserId.Value.ToString("N"),
            audit.ChangedByUserId?.Value,
            changedByUsername,
            audit.PreviousAccessProfileId?.Value,
            audit.PreviousAccessProfileName,
            audit.NewAccessProfileId?.Value,
            audit.NewAccessProfileName,
            SplitCsv(audit.PreviousPermissionCodesCsv),
            SplitCsv(audit.NewPermissionCodesCsv),
            audit.ChangedAt);
    }

    private static IReadOnlyList<string> SplitCsv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();
}

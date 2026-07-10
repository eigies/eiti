using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Branches;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.Users;

internal static class UserEmployeeLinking
{
    // El username de este sistema suele ser un nombre visible ("agustin testa"), no un handle.
    // Lo partimos en nombre/apellido para el Employee auto-creado; sin espacio, se duplica
    // como apellido ya que Employee.Create exige ambos campos no vacios.
    public static (string FirstName, string LastName) SplitUsername(string username)
    {
        var trimmed = username.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            return (trimmed, trimmed);
        }

        var firstName = trimmed[..spaceIndex];
        var lastName = trimmed[(spaceIndex + 1)..].Trim();
        return (firstName, string.IsNullOrWhiteSpace(lastName) ? firstName : lastName);
    }
}

public sealed record UserResponse(
    Guid Id,
    string Username,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    bool IsActive,
    Guid? EmployeeId,
    string? EmployeeName,
    Guid? ProfileId,
    string? ProfileName,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> BranchIds,
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
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid ProfileId,
    Guid? EmployeeId,
    bool IsEmployee = false,
    IReadOnlyList<Guid>? BranchIds = null) : IRequest<Result<UserResponse>>, IRequirePermissions
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
    string FirstName,
    string LastName,
    Guid ProfileId,
    Guid? EmployeeId,
    bool IsEmployee = false,
    IReadOnlyList<Guid>? BranchIds = null) : IRequest<Result<UserResponse>>, IRequirePermissions
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
    private readonly IBranchRepository _branchRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IAccessProfileRepository accessProfileRepository,
        IBranchRepository branchRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _accessProfileRepository = accessProfileRepository;
        _branchRepository = branchRepository;
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

        // El alta de Employee es opt-in via el toggle "Es empleado": no todo usuario del sistema
        // es necesariamente parte de la nomina. El nombre/apellido del Employee sale del propio
        // User (no se re-tipea ni se deriva del username), evitando duplicar ese dato de dominio.
        if (employee is null && request.IsEmployee)
        {
            employee = Employee.Create(_currentUserService.CompanyId, null, request.FirstName, request.LastName, null, null, request.Email, EmployeeRole.Staff);
            await _employeeRepository.AddAsync(employee, cancellationToken);
        }

        var branchResult = await ResolveBranchAccessAsync(request.BranchIds, cancellationToken);
        if (branchResult.IsFailure)
            return Result<UserResponse>.Failure(branchResult.Error);

        var passwordHash = PasswordHash.Create(_passwordHasher.HashPassword(request.Password));

        User user;
        try
        {
            user = User.Create(username, request.FirstName, request.LastName, email, passwordHash, _currentUserService.CompanyId, accessProfile, employee?.Id);
        }
        catch (ArgumentException ex)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Create.InvalidInput", ex.Message));
        }

        user.SetBranchAccess(branchResult.Value);

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

    private async Task<Result<List<BranchId>>> ResolveBranchAccessAsync(IReadOnlyList<Guid>? branchIds, CancellationToken cancellationToken)
    {
        var result = new List<BranchId>();
        if (branchIds is null || branchIds.Count == 0)
            return Result<List<BranchId>>.Success(result);

        var companyBranchIds = (await _branchRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken))
            .Select(b => b.Id.Value)
            .ToHashSet();

        foreach (var id in branchIds.Distinct())
        {
            if (!companyBranchIds.Contains(id))
                return Result<List<BranchId>>.Failure(
                    Error.Validation("Users.InvalidBranch", "Una de las sucursales seleccionadas es inválida."));
            result.Add(new BranchId(id));
        }

        return Result<List<BranchId>>.Success(result);
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
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IUserRoleAuditRepository userRoleAuditRepository,
        IEmployeeRepository employeeRepository,
        IAccessProfileRepository accessProfileRepository,
        IBranchRepository branchRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _userRoleAuditRepository = userRoleAuditRepository;
        _employeeRepository = employeeRepository;
        _accessProfileRepository = accessProfileRepository;
        _branchRepository = branchRepository;
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

        try
        {
            user.UpdateName(request.FirstName, request.LastName);
        }
        catch (ArgumentException ex)
        {
            return Result<UserResponse>.Failure(Error.Validation("Users.Update.InvalidInput", ex.Message));
        }

        user.AssignProfile(accessProfile);

        if (request.EmployeeId.HasValue)
        {
            // Vinculacion explicita a un empleado existente.
            user.LinkEmployee(employee!.Id);
        }
        else if (request.IsEmployee)
        {
            if (user.EmployeeId is null)
            {
                // Toggle "Es empleado" activado sin empleado previo: se crea uno con el nombre actual.
                employee = Employee.Create(_currentUserService.CompanyId, null, request.FirstName, request.LastName, null, null, user.Email.Value, EmployeeRole.Staff);
                await _employeeRepository.AddAsync(employee, cancellationToken);
                user.LinkEmployee(employee.Id);
            }
            else
            {
                // Ya tenia uno vinculado: se sincroniza el nombre (fuente de verdad = User),
                // sin tocar el resto de sus datos propios de Employee.
                employee = await _employeeRepository.GetByIdAsync(user.EmployeeId, _currentUserService.CompanyId, cancellationToken);
                employee?.Update(employee.BranchId, request.FirstName, request.LastName, employee.DocumentNumber, employee.Phone, employee.Email, employee.EmployeeRole);
            }
        }
        else
        {
            // Toggle apagado: se desvincula, sin borrar el registro de Employee (puede tener
            // historial de payroll asociado).
            user.LinkEmployee(null);
        }

        // BranchIds null = no se toca; no-null (incl. vacío) = reemplaza el set.
        if (request.BranchIds is not null)
        {
            var branchResult = await ResolveBranchAccessAsync(request.BranchIds, cancellationToken);
            if (branchResult.IsFailure)
                return Result<UserResponse>.Failure(branchResult.Error);
            user.SetBranchAccess(branchResult.Value);
        }

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

    private async Task<Result<List<BranchId>>> ResolveBranchAccessAsync(IReadOnlyList<Guid> branchIds, CancellationToken cancellationToken)
    {
        var result = new List<BranchId>();
        if (branchIds.Count == 0)
            return Result<List<BranchId>>.Success(result);

        var companyBranchIds = (await _branchRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken))
            .Select(b => b.Id.Value)
            .ToHashSet();

        foreach (var id in branchIds.Distinct())
        {
            if (!companyBranchIds.Contains(id))
                return Result<List<BranchId>>.Failure(
                    Error.Validation("Users.InvalidBranch", "Una de las sucursales seleccionadas es inválida."));
            result.Add(new BranchId(id));
        }

        return Result<List<BranchId>>.Success(result);
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
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email.Value,
            user.IsActive,
            user.EmployeeId?.Value,
            employee?.FullName,
            profile.Id.Value,
            profile.Name,
            permissions,
            user.BranchAccessIds.ToList(),
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

// Backfill puntual para usuarios que ya existian antes de que CreateUser empezara a
// auto-crear el Employee vinculado. Idempotente: solo procesa usuarios con EmployeeId nulo,
// asi que correrlo de nuevo no duplica nada.
public sealed record BackfillEmployeesForUsersCommand() : IRequest<Result<BackfillEmployeesForUsersResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record BackfillEmployeesForUsersResponse(int CreatedCount, IReadOnlyList<Guid> UserIds);

public sealed class BackfillEmployeesForUsersHandler : IRequestHandler<BackfillEmployeesForUsersCommand, Result<BackfillEmployeesForUsersResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BackfillEmployeesForUsersHandler(
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

    public async Task<Result<BackfillEmployeesForUsersResponse>> Handle(BackfillEmployeesForUsersCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BackfillEmployeesForUsersResponse>.Failure(authCheck.Error);

        if (_currentUserService.CompanyId is null)
            return Result<BackfillEmployeesForUsersResponse>.Failure(
                Error.Unauthorized("Users.Backfill.Unauthorized", "Authentication is required."));

        var companyId = _currentUserService.CompanyId;
        var users = await _userRepository.ListByCompanyAsync(companyId, cancellationToken);
        var updatedUserIds = new List<Guid>();

        foreach (var user in users.Where(u => u.EmployeeId is null))
        {
            var (firstName, lastName) = UserEmployeeLinking.SplitUsername(user.Username.Value);
            var employee = Employee.Create(companyId, null, firstName, lastName, null, null, user.Email.Value, EmployeeRole.Staff);
            await _employeeRepository.AddAsync(employee, cancellationToken);
            user.LinkEmployee(employee.Id);
            updatedUserIds.Add(user.Id.Value);
        }

        if (updatedUserIds.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BackfillEmployeesForUsersResponse>.Success(
            new BackfillEmployeesForUsersResponse(updatedUserIds.Count, updatedUserIds));
    }
}

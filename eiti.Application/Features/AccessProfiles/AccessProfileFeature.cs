using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Users;
using MediatR;

namespace eiti.Application.Features.AccessProfiles;

public sealed record AccessProfileResponse(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes,
    bool IsSystem,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ListAccessProfilesQuery() : IRequest<Result<IReadOnlyList<AccessProfileResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed record CreateAccessProfileCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes) : IRequest<Result<AccessProfileResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [eiti.Application.Common.Authorization.PermissionCodes.UsersManage];
}

public sealed record UpdateAccessProfileCommand(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes) : IRequest<Result<AccessProfileResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [eiti.Application.Common.Authorization.PermissionCodes.UsersManage];
}

public sealed record DeleteAccessProfileCommand(Guid Id) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.UsersManage];
}

public sealed class ListAccessProfilesHandler : IRequestHandler<ListAccessProfilesQuery, Result<IReadOnlyList<AccessProfileResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessProfileRepository _accessProfileRepository;

    public ListAccessProfilesHandler(ICurrentUserService currentUserService, IAccessProfileRepository accessProfileRepository)
    {
        _currentUserService = currentUserService;
        _accessProfileRepository = accessProfileRepository;
    }

    public async Task<Result<IReadOnlyList<AccessProfileResponse>>> Handle(ListAccessProfilesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<AccessProfileResponse>>.Failure(authCheck.Error);

        var profiles = await _accessProfileRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken);
        return Result<IReadOnlyList<AccessProfileResponse>>.Success(profiles.Select(AccessProfileMappings.Map).ToArray());
    }
}

public sealed class CreateAccessProfileHandler : IRequestHandler<CreateAccessProfileCommand, Result<AccessProfileResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccessProfileHandler(
        ICurrentUserService currentUserService,
        IAccessProfileRepository accessProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _accessProfileRepository = accessProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccessProfileResponse>> Handle(CreateAccessProfileCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AccessProfileResponse>.Failure(authCheck.Error);

        var validationError = AccessProfileMappings.ValidateRequest(request.Name, request.PermissionCodes);
        if (validationError is not null)
        {
            return Result<AccessProfileResponse>.Failure(validationError);
        }

        if (await _accessProfileRepository.NameExistsAsync(_currentUserService.CompanyId, request.Name, null, cancellationToken))
        {
            return Result<AccessProfileResponse>.Failure(AccessProfileErrors.NameExists);
        }

        AccessProfile profile;
        try
        {
            profile = AccessProfile.Create(_currentUserService.CompanyId, request.Name, request.Description, request.PermissionCodes);
        }
        catch (ArgumentException ex)
        {
            return Result<AccessProfileResponse>.Failure(Error.Validation("AccessProfiles.Create.InvalidInput", ex.Message));
        }

        await _accessProfileRepository.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AccessProfileResponse>.Success(AccessProfileMappings.Map(profile));
    }
}

public sealed class UpdateAccessProfileHandler : IRequestHandler<UpdateAccessProfileCommand, Result<AccessProfileResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAccessProfileHandler(
        ICurrentUserService currentUserService,
        IAccessProfileRepository accessProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _accessProfileRepository = accessProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccessProfileResponse>> Handle(UpdateAccessProfileCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<AccessProfileResponse>.Failure(authCheck.Error);

        var profile = await _accessProfileRepository.GetByIdAsync(new AccessProfileId(request.Id), cancellationToken);
        if (profile is null || profile.CompanyId != _currentUserService.CompanyId)
        {
            return Result<AccessProfileResponse>.Failure(AccessProfileErrors.NotFound);
        }

        var validationError = AccessProfileMappings.ValidateRequest(request.Name, request.PermissionCodes);
        if (validationError is not null)
        {
            return Result<AccessProfileResponse>.Failure(validationError);
        }

        if (await _accessProfileRepository.NameExistsAsync(_currentUserService.CompanyId, request.Name, profile.Id, cancellationToken))
        {
            return Result<AccessProfileResponse>.Failure(AccessProfileErrors.NameExists);
        }

        try
        {
            profile.UpdateDefinition(request.Name, request.Description, request.PermissionCodes);
        }
        catch (ArgumentException ex)
        {
            return Result<AccessProfileResponse>.Failure(Error.Validation("AccessProfiles.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AccessProfileResponse>.Success(AccessProfileMappings.Map(profile));
    }
}

public sealed class DeleteAccessProfileHandler : IRequestHandler<DeleteAccessProfileCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessProfileRepository _accessProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAccessProfileHandler(
        ICurrentUserService currentUserService,
        IAccessProfileRepository accessProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _accessProfileRepository = accessProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteAccessProfileCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var profile = await _accessProfileRepository.GetByIdAsync(new AccessProfileId(request.Id), cancellationToken);
        if (profile is null || profile.CompanyId != _currentUserService.CompanyId)
        {
            return Result.Failure(AccessProfileErrors.NotFound);
        }

        if (await _accessProfileRepository.HasUsersAssignedAsync(profile.Id, cancellationToken))
        {
            return Result.Failure(AccessProfileErrors.InUse);
        }

        _accessProfileRepository.Remove(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class AccessProfileMappings
{
    public static AccessProfileResponse Map(AccessProfile profile) =>
        new(
            profile.Id.Value,
            profile.Name,
            profile.Description,
            profile.Permissions.Select(permission => permission.PermissionCode).OrderBy(permission => permission).ToArray(),
            profile.IsSystem,
            profile.CreatedAt,
            profile.UpdatedAt);

    public static Error? ValidateRequest(string name, IEnumerable<string> permissionCodes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("AccessProfiles.Validation.NameRequired", "Profile name is required.");
        }

        var normalizedPermissions = permissionCodes
            .Where(permissionCode => !string.IsNullOrWhiteSpace(permissionCode))
            .Select(permissionCode => permissionCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPermissions.Length == 0)
        {
            return Error.Validation("AccessProfiles.Validation.PermissionRequired", "Select at least one permission.");
        }

        if (normalizedPermissions.Any(permissionCode => !PermissionCatalog.IsValid(permissionCode)))
        {
            return Error.Validation("AccessProfiles.Validation.InvalidPermission", "One or more selected permissions are invalid.");
        }

        return null;
    }
}

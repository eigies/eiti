using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using eiti.Application.Abstractions.Services;
using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IReadOnlyCollection<string>? _roles;
    private IReadOnlyCollection<string>? _permissions;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public UserId? UserId => TryParseUserId();

    public CompanyId? CompanyId => TryParseCompanyId();

    public IReadOnlyCollection<string> Roles => _roles ??= ReadClaimValues(ClaimTypes.Role);

    public IReadOnlyCollection<string> Permissions => _permissions ??= ReadClaimValues("permission");

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private UserId? TryParseUserId()
    {
        var value = FindClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? new UserId(id) : null;
    }

    private CompanyId? TryParseCompanyId()
    {
        var value = FindClaimValue("company_id");
        return Guid.TryParse(value, out var id) ? new CompanyId(id) : null;
    }

    private string? FindClaimValue(params string[] claimTypes)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private IReadOnlyCollection<string> ReadClaimValues(string claimType)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return Array.Empty<string>();
        }

        return user.Claims
            .Where(claim => claim.Type == claimType && !string.IsNullOrWhiteSpace(claim.Value))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

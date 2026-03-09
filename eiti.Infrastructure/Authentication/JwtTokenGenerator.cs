using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using eiti.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace eiti.Infrastructure.Authentication;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateToken(User user)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
            SecurityAlgorithms.HmacSha256);

        var roleCodes = user.Roles.Select(role => role.RoleCode).ToArray();
        var permissionCodes = RoleCatalog.PermissionsFor(roleCodes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username.Value),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim("company_id", user.CompanyId.Value.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roleCodes.Select(roleCode => new Claim(ClaimTypes.Role, roleCode)));
        claims.AddRange(permissionCodes.Select(permissionCode => new Claim("permission", permissionCode)));

        var securityToken = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            claims: claims,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(securityToken);
    }
}

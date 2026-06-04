using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Abstractions.Services;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    UserId? UserId { get; }
    CompanyId? CompanyId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);

    /// <summary>Sucursales asignadas al usuario (claims "branch"). Vacío = sin restricción.</summary>
    IReadOnlyCollection<Guid> AllowedBranchIds { get; }

    /// <summary>True si ve todas las sucursales: tiene branches.view_all O no tiene ninguna asignada (restricción opt-in).</summary>
    bool CanViewAllBranches { get; }
}

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
}

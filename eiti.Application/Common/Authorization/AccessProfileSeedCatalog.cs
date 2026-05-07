using eiti.Domain.Companies;
using eiti.Domain.Users;

namespace eiti.Application.Common.Authorization;

public static class AccessProfileSeedCatalog
{
    public static IReadOnlyList<AccessProfile> CreateInitialProfiles(CompanyId companyId) =>
        RoleCatalog.All
            .Select(role => AccessProfile.Create(
                companyId,
                role.Name,
                role.Description,
                role.Permissions,
                isSystem: true,
                systemKey: role.Code))
            .ToArray();
}

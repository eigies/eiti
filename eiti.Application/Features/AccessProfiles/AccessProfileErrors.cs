using eiti.Application.Common;

namespace eiti.Application.Features.AccessProfiles;

public static class AccessProfileErrors
{
    public static readonly Error NameExists = Error.Conflict(
        "AccessProfiles.NameExists",
        "An access profile with the same name already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "AccessProfiles.NotFound",
        "The requested access profile was not found.");

    public static readonly Error InUse = Error.Conflict(
        "AccessProfiles.InUse",
        "The access profile is assigned to one or more users.");
}

namespace eiti.Application.Common.Authorization;

public interface IRequirePermissions
{
    IReadOnlyCollection<string> RequiredPermissions { get; }
}

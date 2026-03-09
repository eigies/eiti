using eiti.Application.Abstractions.Services;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUserService;

    public AuthorizationBehavior(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRequirePermissions authorizableRequest)
        {
            return await next();
        }

        if (!_currentUserService.IsAuthenticated)
        {
            return CreateFailure(Error.Unauthorized("Authorization.Unauthorized", "The current user is not authenticated."));
        }

        foreach (var permission in authorizableRequest.RequiredPermissions)
        {
            if (!_currentUserService.HasPermission(permission))
            {
                return CreateFailure(Error.Forbidden("Authorization.Forbidden", "The current user does not have the required permission."));
            }
        }

        return await next();
    }

    private static TResponse CreateFailure(Error error)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var method = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), [typeof(Error)]);

            if (method is not null)
            {
                return (TResponse)method.Invoke(null, [error])!;
            }
        }

        throw new InvalidOperationException($"Cannot create failure result for {responseType.Name}.");
    }
}

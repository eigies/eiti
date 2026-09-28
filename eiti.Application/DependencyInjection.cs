using System.Reflection;
using eiti.Application.Common.Behaviors;
using eiti.Application.Features.Sales.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace eiti.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(AuditBehavior<,>));
            config.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISaleInvoicingService, SaleInvoicingService>();

        return services;
    }
}

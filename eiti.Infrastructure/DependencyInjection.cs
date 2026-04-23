using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Infrastructure.Authentication;
using eiti.Infrastructure.Persistence;
using eiti.Infrastructure.Persistence.Repositories;
using eiti.Infrastructure.Services;
using eiti.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

namespace eiti.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<WhatsAppDispatchOptions>(configuration.GetSection(WhatsAppDispatchOptions.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<ResendClientOptions>(o =>
            o.ApiToken = configuration[$"{EmailSettings.SectionName}:ApiKey"] ?? string.Empty);
        services.AddHttpClient<ResendClient>();
        services.AddTransient<IResend, ResendClient>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IBankRepository, BankRepository>();
        services.AddScoped<IChequeRepository, ChequeRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IBranchProductStockRepository, BranchProductStockRepository>();
        services.AddScoped<ICashDrawerRepository, CashDrawerRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyOnboardingRepository, CompanyOnboardingRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDriverProfileRepository, DriverProfileRepository>();
        services.AddScoped<IFleetLogRepository, FleetLogRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleTransportAssignmentRepository, SaleTransportAssignmentRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleAuditRepository, UserRoleAuditRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpClient<IWhatsAppNotificationService, WhatsAppNotificationService>();

        return services;
    }
}

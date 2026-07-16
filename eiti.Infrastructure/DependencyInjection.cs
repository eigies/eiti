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
using Npgsql;
using Resend;

namespace eiti.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Npgsql 6+ mapea DateTime a 'timestamp with time zone' y exige DateTimeKind.Utc, lo que
        // rompe los muchos DateTime con Kind=Unspecified que ya existen en el código. El switch legacy
        // restaura el comportamiento previo (DateTime <-> 'timestamp without time zone', sin exigir Kind),
        // evitando tocar decenas de usos. Debe setearse antes de crear el data source de Npgsql.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var connectionString = NormalizePostgresConnectionString(
            configuration.GetConnectionString("DefaultConnection"));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                b => b
                    .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                    // Evita la explosión cartesiana cuando una query hace Include de varias
                    // colecciones (Details + Payments + TradeIns + CcPayments): EF corre un
                    // query por colección en vez de un JOIN gigante que estalla la RAM del API.
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<WhatsAppDispatchOptions>(configuration.GetSection(WhatsAppDispatchOptions.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<ResendClientOptions>(o =>
            o.ApiToken = configuration[$"{EmailSettings.SectionName}:ApiKey"] ?? string.Empty);
        services.AddHttpClient<ResendClient>();
        services.AddTransient<IResend, ResendClient>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IAccessProfileRepository, AccessProfileRepository>();
        services.AddScoped<IBankRepository, BankRepository>();
        services.AddScoped<IChequeRepository, ChequeRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IBranchProductStockRepository, BranchProductStockRepository>();
        services.AddScoped<ICashDrawerRepository, CashDrawerRepository>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyOnboardingRepository, CompanyOnboardingRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDriverProfileRepository, DriverProfileRepository>();
        services.AddScoped<IFleetLogRepository, FleetLogRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleTransportAssignmentRepository, SaleTransportAssignmentRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleAuditRepository, UserRoleAuditRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISupplierPaymentRepository, SupplierPaymentRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IPayrollDeductionConceptRepository, PayrollDeductionConceptRepository>();
        services.AddScoped<IPayrollAdvanceRepository, PayrollAdvanceRepository>();
        services.AddScoped<IPayrollBonusConceptRepository, PayrollBonusConceptRepository>();
        services.AddScoped<IPayrollBonusRepository, PayrollBonusRepository>();
        services.AddScoped<IPayrollLiquidationRepository, PayrollLiquidationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditSnapshotService, AuditSnapshotService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpClient<IWhatsAppNotificationService, WhatsAppNotificationService>();

        return services;
    }

    // Railway/Heroku/etc entregan la conexión Postgres como URL (postgresql://user:pass@host:port/db),
    // pero Npgsql espera el formato keyword (Host=...;Username=...;...). Si llega una URL, la convierte;
    // si ya viene en formato keyword, la deja igual.
    private static string? NormalizePostgresConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            // Prefer: usa TLS si el servidor lo ofrece (proxy público) y texto plano si no (red privada).
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
}

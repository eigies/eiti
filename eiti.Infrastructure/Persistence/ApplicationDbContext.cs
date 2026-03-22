using eiti.Domain.Addresses;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Fleet;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Transport;
using eiti.Domain.Users;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<CashDrawer> CashDrawers { get; set; }
    public DbSet<CashSession> CashSessions { get; set; }
    public DbSet<CashMovement> CashMovements { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<CompanyOnboarding> CompanyOnboarding { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<BranchProductStock> BranchProductStocks { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<DriverProfile> DriverProfiles { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<FleetLog> FleetLogs { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleDetail> SaleDetails { get; set; }
    public DbSet<SalePayment> SalePayments { get; set; }
    public DbSet<SaleTradeIn> SaleTradeIns { get; set; }
    public DbSet<SaleCcPayment> SaleCcPayments { get; set; }
    public DbSet<SaleTransportAssignment> SaleTransportAssignments { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRoleAssignment> UserRoles { get; set; }
    public DbSet<UserRoleAudit> UserRoleAudits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

using eiti.Domain.Addresses;
using eiti.Domain.Audit;
using eiti.Domain.Banks;
using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Fleet;
using eiti.Domain.Payroll;
using eiti.Domain.Products;
using eiti.Domain.Purchases;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Suppliers;
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

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankInstallmentPlan> BankInstallmentPlans => Set<BankInstallmentPlan>();
    public DbSet<Cheque> Cheques => Set<Cheque>();
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<CashDrawer> CashDrawers { get; set; }
    public DbSet<CashDrawerUserAssignment> CashDrawerUserAssignments { get; set; }
    public DbSet<CashSession> CashSessions { get; set; }
    public DbSet<CashMovement> CashMovements { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<Company> Companies { get; set; }
    public DbSet<CompanyOnboarding> CompanyOnboarding { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
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
    public DbSet<AccessProfile> AccessProfiles { get; set; }
    public DbSet<AccessProfilePermission> AccessProfilePermissions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserBranchAccess> UserBranchAccesses => Set<UserBranchAccess>();
    public DbSet<UserRoleAudit> UserRoleAudits { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseDetail> PurchaseDetails => Set<PurchaseDetail>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PayrollDeductionConcept> PayrollDeductionConcepts => Set<PayrollDeductionConcept>();
    public DbSet<PayrollAdvance> PayrollAdvances => Set<PayrollAdvance>();
    public DbSet<PayrollLiquidation> PayrollLiquidations => Set<PayrollLiquidation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

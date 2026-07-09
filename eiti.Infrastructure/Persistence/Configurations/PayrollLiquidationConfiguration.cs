using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollLiquidationConfiguration : IEntityTypeConfiguration<PayrollLiquidation>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidation> builder)
    {
        builder.ToTable("PayrollLiquidations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollLiquidationId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.BranchId).HasConversion(id => id!.Value, value => new BranchId(value)).IsRequired(false);
        builder.Property(x => x.PeriodLabel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PeriodStart).IsRequired();
        builder.Property(x => x.PeriodEnd).IsRequired();
        builder.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<int?>().IsRequired(false);
        builder.Property(x => x.PaidAt).IsRequired(false);
        builder.Property(x => x.CashSessionId).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Ignore(x => x.NetAmount);

        // Único por (empresa, empleado, período) mientras la liquidación no esté cancelada
        // (Status = 3). Mismo patrón de índice único filtrado que SaleTransportAssignment.
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.PeriodLabel })
            .HasFilter("\"Status\" <> 3")
            .IsUnique();

        builder.HasMany(x => x.DeductionLines)
            .WithOne()
            .HasForeignKey(l => l.PayrollLiquidationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AdvanceLines)
            .WithOne()
            .HasForeignKey(l => l.PayrollLiquidationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.DeductionLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.AdvanceLines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PayrollLiquidationDeductionLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationDeductionLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationDeductionLine> builder)
    {
        builder.ToTable("PayrollLiquidationDeductionLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id.Value, value => new PayrollLiquidationId(value))
            .IsRequired();
        builder.Property(x => x.ConceptName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Percentage).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}

public sealed class PayrollLiquidationAdvanceLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationAdvanceLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationAdvanceLine> builder)
    {
        builder.ToTable("PayrollLiquidationAdvanceLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id.Value, value => new PayrollLiquidationId(value))
            .IsRequired();
        builder.Property(x => x.PayrollAdvanceId).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}

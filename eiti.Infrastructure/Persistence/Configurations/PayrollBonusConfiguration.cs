using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollBonusConfiguration : IEntityTypeConfiguration<PayrollBonus>
{
    public void Configure(EntityTypeBuilder<PayrollBonus> builder)
    {
        builder.ToTable("PayrollBonuses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollBonusId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.ConceptId).HasConversion(id => id.Value, value => new PayrollBonusConceptId(value)).IsRequired();
        builder.Property(x => x.AmountType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Value).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id!.Value, value => new PayrollLiquidationId(value))
            .IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Status });
    }
}

public sealed class PayrollLiquidationBonusLineConfiguration : IEntityTypeConfiguration<PayrollLiquidationBonusLine>
{
    public void Configure(EntityTypeBuilder<PayrollLiquidationBonusLine> builder)
    {
        builder.ToTable("PayrollLiquidationBonusLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PayrollLiquidationId)
            .HasConversion(id => id.Value, value => new PayrollLiquidationId(value))
            .IsRequired();
        builder.Property(x => x.PayrollBonusId).IsRequired();
        builder.Property(x => x.ConceptName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AmountType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Value).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
    }
}

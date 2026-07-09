using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Payroll;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollAdvanceConfiguration : IEntityTypeConfiguration<PayrollAdvance>
{
    public void Configure(EntityTypeBuilder<PayrollAdvance> builder)
    {
        builder.ToTable("PayrollAdvances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollAdvanceId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Date).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AppliedToLiquidationId)
            .HasConversion(id => id!.Value, value => new PayrollLiquidationId(value))
            .IsRequired(false);
        builder.Property(x => x.CreatedByUserId).HasConversion(id => id.Value, value => new UserId(value)).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Status });
    }
}

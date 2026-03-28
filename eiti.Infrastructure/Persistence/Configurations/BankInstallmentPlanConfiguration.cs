using eiti.Domain.Banks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class BankInstallmentPlanConfiguration : IEntityTypeConfiguration<BankInstallmentPlan>
{
    public void Configure(EntityTypeBuilder<BankInstallmentPlan> builder)
    {
        builder.ToTable("BankInstallmentPlans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.BankId).IsRequired();

        builder.Property(p => p.Cuotas).IsRequired();

        builder.Property(p => p.SurchargePct)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(p => p.Active)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => new { p.BankId, p.Cuotas }).IsUnique();
    }
}

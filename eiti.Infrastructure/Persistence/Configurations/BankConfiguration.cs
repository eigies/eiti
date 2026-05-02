using eiti.Domain.Banks;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Banks");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedOnAdd();

        builder.Property(b => b.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();
        builder.HasIndex(b => b.CompanyId);

        builder.Property(b => b.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(b => b.Active)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        builder.HasMany(b => b.InstallmentPlans)
            .WithOne()
            .HasForeignKey(p => p.BankId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.InstallmentPlans)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

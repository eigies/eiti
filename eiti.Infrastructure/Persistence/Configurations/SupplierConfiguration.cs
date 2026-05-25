using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).IsRequired().ValueGeneratedNever();
        builder.Property(s => s.CompanyId).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(50).IsRequired(false);
        builder.Property(s => s.Email).HasMaxLength(200).IsRequired(false);
        builder.Property(s => s.TaxId).HasMaxLength(50).IsRequired(false);
        builder.Property(s => s.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.CompanyId);
        builder.HasIndex(s => new { s.CompanyId, s.Name });
    }
}

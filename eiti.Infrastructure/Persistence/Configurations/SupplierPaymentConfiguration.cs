using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("SupplierPayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.SupplierId).IsRequired();
        builder.Property(p => p.BranchId).IsRequired();
        builder.Property(p => p.Method).HasConversion<int>().IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.ChequeId).IsRequired(false);
        builder.Property(p => p.Reference).HasMaxLength(120).IsRequired(false);
        builder.Property(p => p.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.Date).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedByUserId).IsRequired();

        builder.HasIndex(p => new { p.CompanyId, p.SupplierId });
        builder.HasIndex(p => p.Status);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using eiti.Domain.Purchases;
using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.BranchId).IsRequired();
        builder.Property(p => p.SupplierId).IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.InvoiceNumber).HasMaxLength(100).IsRequired(false);
        builder.Property(p => p.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired(false);
        builder.Property(p => p.CancelledAt).IsRequired(false);
        builder.Property(p => p.IvaPct).HasColumnType("decimal(5,2)").IsRequired(false);
        builder.Property(p => p.IngresosBrutosPct).HasColumnType("decimal(5,2)").IsRequired(false);

        builder.Ignore(p => p.TotalAmount);
        builder.Ignore(p => p.TaxAmount);
        builder.Ignore(p => p.GrandTotal);
        builder.Ignore(p => p.TotalPaid);
        builder.Ignore(p => p.PendingAmount);

        builder.HasIndex(p => p.CompanyId);
        builder.HasIndex(p => new { p.CompanyId, p.SupplierId });
        builder.HasIndex(p => new { p.CompanyId, p.CreatedAt });

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Details)
            .WithOne()
            .HasForeignKey(d => d.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Payments)
            .WithOne()
            .HasForeignKey(pay => pay.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Details)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.Payments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

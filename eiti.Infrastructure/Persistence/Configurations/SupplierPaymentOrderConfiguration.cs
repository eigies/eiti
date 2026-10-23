using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SupplierPaymentOrderConfiguration : IEntityTypeConfiguration<SupplierPaymentOrder>
{
    public void Configure(EntityTypeBuilder<SupplierPaymentOrder> builder)
    {
        builder.ToTable("SupplierPaymentOrders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).IsRequired().ValueGeneratedNever();
        builder.Property(o => o.CompanyId).IsRequired();
        builder.Property(o => o.SupplierId).IsRequired();
        builder.Property(o => o.BranchId).IsRequired();
        builder.Property(o => o.Code).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Date).IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.Status).HasConversion<int>().IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.CreatedByUserId).IsRequired();
        builder.Property(o => o.CancelledAt).IsRequired(false);
        builder.Property(o => o.CancelledByUserId).IsRequired(false);

        builder.HasIndex(o => new { o.CompanyId, o.SupplierId });
        builder.HasIndex(o => o.Status);

        // El número sale de un conteo: si dos órdenes simultáneas calculan el mismo, que falle fuerte
        // en vez de quedar dos OP con el mismo número.
        builder.HasIndex(o => new { o.CompanyId, o.Code }).IsUnique();

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

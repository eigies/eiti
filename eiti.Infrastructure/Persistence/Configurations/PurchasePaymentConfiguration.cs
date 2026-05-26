using eiti.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PurchasePaymentConfiguration : IEntityTypeConfiguration<PurchasePayment>
{
    public void Configure(EntityTypeBuilder<PurchasePayment> builder)
    {
        builder.ToTable("PurchasePayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.PurchaseId).IsRequired();
        builder.Property(p => p.Method).HasConversion<int>().IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(120).IsRequired(false);
        builder.Property(p => p.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.Date).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.IvaPct).HasColumnType("decimal(5,2)").IsRequired(false);
        builder.Property(p => p.IngresosBrutosPct).HasColumnType("decimal(5,2)").IsRequired(false);

        builder.HasIndex(p => p.PurchaseId);
    }
}

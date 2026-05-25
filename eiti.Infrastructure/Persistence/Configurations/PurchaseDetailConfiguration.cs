using eiti.Domain.Purchases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PurchaseDetailConfiguration : IEntityTypeConfiguration<PurchaseDetail>
{
    public void Configure(EntityTypeBuilder<PurchaseDetail> builder)
    {
        builder.ToTable("PurchaseDetails");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).IsRequired().ValueGeneratedNever();
        builder.Property(d => d.PurchaseId).IsRequired();
        builder.Property(d => d.ProductId).IsRequired();
        builder.Property(d => d.ProductName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.Quantity).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(d => d.UnitCost).HasColumnType("decimal(18,2)").IsRequired();

        builder.Ignore(d => d.TotalAmount);

        builder.HasIndex(d => d.PurchaseId);
    }
}

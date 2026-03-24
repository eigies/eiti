using eiti.Domain.Products;
using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleDetailConfiguration : IEntityTypeConfiguration<SaleDetail>
{
    public void Configure(EntityTypeBuilder<SaleDetail> builder)
    {
        builder.ToTable("SaleDetails");

        builder.HasKey(detail => new { detail.SaleId, detail.ProductId });

        builder.Property(detail => detail.SaleId)
            .HasConversion(
                id => id.Value,
                value => new SaleId(value))
            .IsRequired();

        builder.Property(detail => detail.ProductId)
            .HasConversion(
                id => id.Value,
                value => new ProductId(value))
            .IsRequired();

        builder.Property(detail => detail.Quantity).IsRequired();

        builder.Property(detail => detail.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(detail => detail.DiscountPercent)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(detail => detail.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(detail => detail.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using eiti.Domain.Products;
using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleTradeInConfiguration : IEntityTypeConfiguration<SaleTradeIn>
{
    public void Configure(EntityTypeBuilder<SaleTradeIn> builder)
    {
        builder.ToTable("SaleTradeIns");

        builder.HasKey(tradeIn => new { tradeIn.SaleId, tradeIn.ProductId });

        builder.Property(tradeIn => tradeIn.SaleId)
            .HasConversion(
                id => id.Value,
                value => new SaleId(value))
            .IsRequired();

        builder.Property(tradeIn => tradeIn.ProductId)
            .HasConversion(
                id => id.Value,
                value => new ProductId(value))
            .IsRequired();

        builder.Property(tradeIn => tradeIn.Quantity).IsRequired();

        builder.Property(tradeIn => tradeIn.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasOne<Sale>()
            .WithMany(sale => sale.TradeIns)
            .HasForeignKey(tradeIn => tradeIn.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(tradeIn => tradeIn.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

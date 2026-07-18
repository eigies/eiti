using eiti.Domain.Products;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class QuoteDetailConfiguration : IEntityTypeConfiguration<QuoteDetail>
{
    public void Configure(EntityTypeBuilder<QuoteDetail> builder)
    {
        builder.ToTable("QuoteDetails");

        builder.HasKey(detail => new { detail.QuoteId, detail.ProductId });

        builder.Property(detail => detail.QuoteId)
            .HasConversion(id => id.Value, value => new eiti.Domain.Quotes.QuoteId(value))
            .IsRequired();

        builder.Property(detail => detail.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .IsRequired();

        builder.Property(detail => detail.Quantity).IsRequired();
        builder.Property(detail => detail.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(detail => detail.DiscountPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(detail => detail.LineTotal).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasIndex(detail => detail.ProductId);
    }
}

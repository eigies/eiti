using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");

        builder.HasKey(quote => quote.Id);

        builder.Property(quote => quote.Id)
            .HasConversion(id => id.Value, value => new QuoteId(value))
            .IsRequired();

        builder.Property(quote => quote.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(quote => quote.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(quote => quote.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new CustomerId(value.Value) : null)
            .IsRequired(false);

        builder.Property(quote => quote.ProspectName).HasMaxLength(200).IsRequired(false);
        builder.Property(quote => quote.ProspectContact).HasMaxLength(200).IsRequired(false);
        builder.Property(quote => quote.GeneralDiscountPercent).HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(quote => quote.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(quote => quote.ExpiresAt).IsRequired();
        builder.Property(quote => quote.Status).HasColumnName("IdQuoteStatus").HasConversion<int>().IsRequired();
        builder.Property(quote => quote.ConvertedSaleId).IsRequired(false);
        builder.Property(quote => quote.Code).HasMaxLength(20).IsRequired(false);
        builder.Property(quote => quote.CreatedByUserId).IsRequired();
        builder.Property(quote => quote.CreatedAt).IsRequired();

        builder.HasIndex(quote => new { quote.CompanyId, quote.CreatedAt });
        builder.HasIndex(quote => quote.CustomerId);
        builder.HasIndex(quote => quote.BranchId);

        builder.HasOne<Company>().WithMany().HasForeignKey(quote => quote.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(quote => quote.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(quote => quote.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(quote => quote.Details)
            .WithOne()
            .HasForeignKey(detail => detail.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(quote => quote.Details)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

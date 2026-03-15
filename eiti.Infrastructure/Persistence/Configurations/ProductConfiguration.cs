using eiti.Domain.Companies;
using eiti.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasConversion(
                id => id.Value,
                value => new ProductId(value))
            .IsRequired();

        builder.Property(product => product.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(product => product.Brand)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(product => product.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(product => product.Sku)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(product => product.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(product => product.CostPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(product => product.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(product => product.AllowsManualValueInSale)
            .IsRequired();

        builder.Property(product => product.NoDeliverySurcharge)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(product => product.CreatedAt).IsRequired();
        builder.Property(product => product.UpdatedAt).IsRequired(false);

        builder.HasIndex(product => new { product.CompanyId, product.Name })
            .IsUnique();

        builder.HasIndex(product => new { product.CompanyId, product.Code })
            .IsUnique();

        builder.HasIndex(product => new { product.CompanyId, product.Sku })
            .IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(product => product.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

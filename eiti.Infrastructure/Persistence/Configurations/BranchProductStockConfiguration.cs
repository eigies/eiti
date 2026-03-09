using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class BranchProductStockConfiguration : IEntityTypeConfiguration<BranchProductStock>
{
    public void Configure(EntityTypeBuilder<BranchProductStock> builder)
    {
        builder.ToTable("BranchProductStocks");

        builder.HasKey(stock => stock.Id);

        builder.Property(stock => stock.Id)
            .HasConversion(id => id.Value, value => new BranchProductStockId(value))
            .IsRequired();

        builder.Property(stock => stock.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(stock => stock.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(stock => stock.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .IsRequired();

        builder.Property(stock => stock.OnHandQuantity).IsRequired();
        builder.Property(stock => stock.ReservedQuantity).IsRequired();
        builder.Property(stock => stock.UpdatedAt).IsRequired();

        builder.HasIndex(stock => new { stock.BranchId, stock.ProductId }).IsUnique();
        builder.HasIndex(stock => new { stock.CompanyId, stock.BranchId });
        builder.HasIndex(stock => new { stock.CompanyId, stock.ProductId });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(stock => stock.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(stock => stock.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(stock => stock.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

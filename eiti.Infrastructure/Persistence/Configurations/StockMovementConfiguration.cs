using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .HasConversion(id => id.Value, value => new StockMovementId(value))
            .IsRequired();

        builder.Property(movement => movement.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(movement => movement.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(movement => movement.ProductId)
            .HasConversion(id => id.Value, value => new ProductId(value))
            .IsRequired();

        builder.Property(movement => movement.BranchProductStockId)
            .HasConversion(id => id.Value, value => new BranchProductStockId(value))
            .IsRequired();

        builder.Property(movement => movement.Type).IsRequired();
        builder.Property(movement => movement.Quantity).IsRequired();
        builder.Property(movement => movement.ReferenceType).HasMaxLength(50).IsRequired(false);
        builder.Property(movement => movement.Description).HasMaxLength(255).IsRequired(false);
        builder.Property(movement => movement.CreatedAt).IsRequired();

        builder.Property(movement => movement.CreatedByUserId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new UserId(value.Value) : null)
            .IsRequired(false);

        builder.HasIndex(movement => new { movement.BranchId, movement.ProductId, movement.CreatedAt });
        builder.HasIndex(movement => new { movement.CompanyId, movement.CreatedAt });
        builder.HasIndex(movement => movement.ReferenceId);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(movement => movement.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(movement => movement.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BranchProductStock>()
            .WithMany()
            .HasForeignKey(movement => movement.BranchProductStockId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

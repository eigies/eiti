using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(sale => sale.Id);

        builder.Property(sale => sale.Id)
            .HasConversion(
                id => id.Value,
                value => new SaleId(value))
            .IsRequired();

        builder.Property(sale => sale.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(sale => sale.BranchId)
            .HasConversion(
                id => id.Value,
                value => new BranchId(value))
            .IsRequired();

        builder.Property(sale => sale.CustomerId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new CustomerId(value.Value) : null)
            .IsRequired(false);

        builder.Property(sale => sale.CashSessionId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new CashSessionId(value.Value) : null)
            .IsRequired(false);

        builder.Property(sale => sale.TransportAssignmentId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new SaleTransportAssignmentId(value.Value) : null)
            .IsRequired(false);

        builder.Property(sale => sale.SaleStatus)
            .HasColumnName("IdSaleStatus")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(sale => sale.HasDelivery).IsRequired();
        builder.Property(sale => sale.NoDeliverySurchargeTotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(sale => sale.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(sale => sale.CreatedAt).IsRequired();
        builder.Property(sale => sale.PaidAt).IsRequired(false);
        builder.Property(sale => sale.UpdatedAt).IsRequired(false);
        builder.Property(sale => sale.IsModified).IsRequired();

        builder.HasIndex(sale => new { sale.CompanyId, sale.CreatedAt });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(sale => sale.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(sale => sale.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(sale => sale.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CashSession>()
            .WithMany()
            .HasForeignKey(sale => sale.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SaleTransportAssignment>()
            .WithMany()
            .HasForeignKey(sale => sale.TransportAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sale => sale.Details)
            .WithOne()
            .HasForeignKey(detail => detail.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(sale => sale.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(sale => sale.TradeIns)
            .WithOne()
            .HasForeignKey(tradeIn => tradeIn.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(sale => sale.Details)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(sale => sale.Payments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(sale => sale.TradeIns)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

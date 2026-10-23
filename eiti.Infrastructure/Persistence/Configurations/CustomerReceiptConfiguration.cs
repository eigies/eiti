using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CustomerReceiptConfiguration : IEntityTypeConfiguration<CustomerReceipt>
{
    public void Configure(EntityTypeBuilder<CustomerReceipt> builder)
    {
        builder.ToTable("CustomerReceipts");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).IsRequired().ValueGeneratedNever();
        builder.Property(r => r.CompanyId).IsRequired();
        builder.Property(r => r.CustomerId).IsRequired();
        builder.Property(r => r.BranchId).IsRequired();
        builder.Property(r => r.Code).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Date).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(r => r.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedByUserId).IsRequired();
        builder.Property(r => r.CancelledAt).IsRequired(false);
        builder.Property(r => r.CancelledByUserId).IsRequired(false);

        builder.HasIndex(r => new { r.CompanyId, r.CustomerId });
        builder.HasIndex(r => r.Status);

        // El número sale de un conteo: si dos recibos simultáneos calculan el mismo, que falle fuerte
        // en vez de quedar dos RC con el mismo número.
        builder.HasIndex(r => new { r.CompanyId, r.Code }).IsUnique();

        // Sin FK navegable a Customer (su PK es el value object CustomerId), igual que CustomerPayment.
    }
}

using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CustomerCreditNoteConfiguration : IEntityTypeConfiguration<CustomerCreditNote>
{
    public void Configure(EntityTypeBuilder<CustomerCreditNote> builder)
    {
        builder.ToTable("CustomerCreditNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired().ValueGeneratedNever();
        builder.Property(n => n.CompanyId).IsRequired();
        builder.Property(n => n.CustomerId).IsRequired();
        builder.Property(n => n.BranchId).IsRequired();
        builder.Property(n => n.Code).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(n => n.Reason).HasMaxLength(250).IsRequired();
        builder.Property(n => n.Date).IsRequired();
        builder.Property(n => n.SaleId).IsRequired(false);
        builder.Property(n => n.Status).HasConversion<int>().IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.CreatedByUserId).IsRequired();
        builder.Property(n => n.CancelledAt).IsRequired(false);
        builder.Property(n => n.CancelledByUserId).IsRequired(false);

        builder.HasIndex(n => new { n.CompanyId, n.CustomerId });
        builder.HasIndex(n => n.Status);

        // No se modela FK navegable a Customer: su PK es el value object CustomerId y aquí es Guid
        // (mismo patrón que CustomerPaymentConfiguration).
    }
}

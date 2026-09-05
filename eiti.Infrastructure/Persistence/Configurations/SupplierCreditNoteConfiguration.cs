using eiti.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SupplierCreditNoteConfiguration : IEntityTypeConfiguration<SupplierCreditNote>
{
    public void Configure(EntityTypeBuilder<SupplierCreditNote> builder)
    {
        builder.ToTable("SupplierCreditNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).IsRequired().ValueGeneratedNever();
        builder.Property(n => n.CompanyId).IsRequired();
        builder.Property(n => n.SupplierId).IsRequired();
        builder.Property(n => n.BranchId).IsRequired();
        builder.Property(n => n.Code).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(n => n.Reason).HasMaxLength(250).IsRequired();
        builder.Property(n => n.Date).IsRequired();
        builder.Property(n => n.PurchaseId).IsRequired(false);
        builder.Property(n => n.Status).HasConversion<int>().IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.CreatedByUserId).IsRequired();
        builder.Property(n => n.CancelledAt).IsRequired(false);
        builder.Property(n => n.CancelledByUserId).IsRequired(false);

        builder.HasIndex(n => new { n.CompanyId, n.SupplierId });
        builder.HasIndex(n => n.Status);

        // No se modela FK navegable a Supplier: su PK es el value object SupplierId y aquí es Guid
        // (mismo patrón que SupplierPaymentConfiguration).
    }
}

using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CustomerPaymentConfiguration : IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<CustomerPayment> builder)
    {
        builder.ToTable("CustomerPayments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.CustomerId).IsRequired();
        builder.Property(p => p.BranchId).IsRequired();
        builder.Property(p => p.Method).HasConversion<int>().IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.ChequeId).IsRequired(false);
        builder.Property(p => p.Reference).HasMaxLength(120).IsRequired(false);
        builder.Property(p => p.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.Date).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedByUserId).IsRequired();

        builder.Property(p => p.CardBankId).IsRequired(false);
        builder.Property(p => p.CardCuotas).IsRequired(false);
        builder.Property(p => p.CardSurchargePct).HasColumnType("decimal(5,2)").IsRequired(false);
        builder.Property(p => p.CardSurchargeAmt).HasColumnType("decimal(18,2)").IsRequired(false);
        builder.Property(p => p.TotalCobrado).HasColumnType("decimal(18,2)").IsRequired(false);

        builder.HasIndex(p => new { p.CompanyId, p.CustomerId });
        builder.HasIndex(p => p.Status);

        // No se modela FK navegable a Customer: su PK es el value object CustomerId y aquí CustomerId es Guid
        // (mismo patrón que SaleCcPayment, que no navega a Customer). El índice por CustomerId cubre las consultas.
    }
}

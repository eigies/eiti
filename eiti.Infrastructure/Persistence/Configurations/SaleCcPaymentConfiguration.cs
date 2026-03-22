using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleCcPaymentConfiguration : IEntityTypeConfiguration<SaleCcPayment>
{
    public void Configure(EntityTypeBuilder<SaleCcPayment> builder)
    {
        builder.ToTable("SaleCcPayments");

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id)
            .HasConversion(
                id => id.Value,
                value => new SaleCcPaymentId(value))
            .IsRequired();

        builder.Property(payment => payment.SaleId)
            .HasConversion(
                id => id.Value,
                value => new SaleId(value))
            .IsRequired();

        builder.Property(payment => payment.Method)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(payment => payment.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(payment => payment.Date).IsRequired();

        builder.Property(payment => payment.Notes)
            .HasMaxLength(250)
            .IsRequired(false);

        builder.Property(payment => payment.Status)
            .HasConversion<int>()
            .HasDefaultValue(SaleCcPaymentStatus.Active)
            .IsRequired();

        builder.Property(payment => payment.CreatedAt).IsRequired();

        builder.Property(payment => payment.CancelledAt).IsRequired(false);

        builder.HasIndex(payment => payment.SaleId);
    }
}

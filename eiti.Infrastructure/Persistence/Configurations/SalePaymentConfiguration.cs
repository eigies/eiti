using eiti.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");

        builder.HasKey(payment => new { payment.SaleId, payment.Method });

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

        builder.Property(payment => payment.Reference)
            .HasMaxLength(120)
            .IsRequired(false);

        builder.Property(p => p.CardBankId).IsRequired(false);
        builder.Property(p => p.CardCuotas).IsRequired(false);
        builder.Property(p => p.CardSurchargePct).HasColumnType("decimal(5,2)").IsRequired(false);
        builder.Property(p => p.CardSurchargeAmt).HasColumnType("decimal(18,2)").IsRequired(false);
        builder.Property(p => p.TotalCobrado).HasColumnType("decimal(18,2)").IsRequired(false);

        builder.HasOne<Sale>()
            .WithMany(sale => sale.Payments)
            .HasForeignKey(payment => payment.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

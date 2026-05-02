using eiti.Domain.Cheques;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        builder.ToTable("Cheques");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();
        builder.HasIndex(c => c.CompanyId);

        builder.Property(c => c.SalePaymentSaleId).IsRequired(false);
        builder.Property(c => c.SalePaymentMethod).IsRequired(false);
        builder.Property(c => c.SaleCcPaymentId).IsRequired(false);

        builder.Property(c => c.BankId).IsRequired();

        builder.Property(c => c.Numero)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Titular)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.CuitDni)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Monto)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(c => c.FechaEmision)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.FechaVencimiento)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.Estado)
            .HasConversion<int>()
            .HasDefaultValue(ChequeStatus.EnCartera)
            .IsRequired();

        builder.Property(c => c.Notas)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => c.SalePaymentSaleId);
        builder.HasIndex(c => c.SaleCcPaymentId);
        builder.HasIndex(c => c.BankId);
        builder.HasIndex(c => c.Estado);
        builder.HasIndex(c => c.FechaVencimiento);
    }
}

using eiti.Domain.Cash;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements");

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Id)
            .HasConversion(id => id.Value, value => new CashMovementId(value))
            .IsRequired();

        builder.Property(movement => movement.CashSessionId)
            .HasConversion(id => id.Value, value => new CashSessionId(value))
            .IsRequired();

        builder.Property(movement => movement.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.Direction)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(movement => movement.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(movement => movement.OccurredAt).IsRequired();

        builder.Property(movement => movement.ReferenceType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(movement => movement.ReferenceId).IsRequired(false);

        builder.Property(movement => movement.Description)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(movement => movement.CreatedByUserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(movement => movement.CcPaymentGroupId).IsRequired(false);

        builder.Property(movement => movement.TransferCounterpartSessionId).IsRequired(false);

        builder.Property(movement => movement.OriginalCashSessionId).IsRequired(false);

        builder.Property(movement => movement.PaymentMethod).IsRequired(false);

        builder.Property(movement => movement.SaleCcPaymentId).IsRequired(false);

        builder.Property(movement => movement.SupplierPaymentId).IsRequired(false);

        builder.Property(movement => movement.CustomerPaymentId).IsRequired(false);

        builder.Property(movement => movement.PayrollLiquidationId).IsRequired(false);

        builder.Property(movement => movement.PayrollAdvanceId).IsRequired(false);

        builder.HasIndex(movement => new { movement.CashSessionId, movement.OccurredAt });

        builder.HasIndex(movement => movement.CcPaymentGroupId);

        builder.HasIndex(movement => movement.SaleCcPaymentId);

        builder.HasIndex(movement => movement.SupplierPaymentId);

        builder.HasIndex(movement => movement.CustomerPaymentId);

        builder.HasIndex(movement => movement.PayrollLiquidationId);

        builder.HasIndex(movement => movement.PayrollAdvanceId);

        // Anti-duplicado: una venta directa produce a lo sumo UN movimiento de ingreso por tipo
        // (efectivo=2, transferencia=10, tarjeta=11). Un doble submit concurrente que intente
        // insertar un segundo movimiento de la misma venta+tipo viola este índice y la transacción
        // se aborta (en vez de duplicar el ingreso en la caja). Los pagos de cuenta corriente (tipo 8)
        // quedan fuera del filtro porque una venta puede tener varios a lo largo del tiempo.
        builder.HasIndex(movement => new { movement.ReferenceId, movement.Type })
            .IsUnique()
            .HasFilter("\"ReferenceType\" = 'Sale' AND \"Type\" IN (2, 10, 11)");
    }
}

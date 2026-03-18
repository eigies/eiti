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

        builder.Property(movement => movement.TransferCounterpartSessionId).IsRequired(false);

        builder.HasIndex(movement => new { movement.CashSessionId, movement.OccurredAt });
    }
}

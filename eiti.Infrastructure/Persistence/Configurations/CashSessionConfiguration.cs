using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("CashSessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasConversion(id => id.Value, value => new CashSessionId(value))
            .IsRequired();

        builder.Property(session => session.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(session => session.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(session => session.CashDrawerId)
            .HasConversion(id => id.Value, value => new CashDrawerId(value))
            .IsRequired();

        builder.Property(session => session.OpenedByUserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(session => session.ClosedByUserId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new UserId(value.Value) : null)
            .IsRequired(false);

        builder.Property(session => session.OpenedAt).IsRequired();
        builder.Property(session => session.ClosedAt).IsRequired(false);
        builder.Property(session => session.OpeningAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(session => session.ActualClosingAmount).HasColumnType("decimal(18,2)").IsRequired(false);
        builder.Property(session => session.Status).HasConversion<int>().IsRequired();
        builder.Property(session => session.Notes).HasMaxLength(255).IsRequired(false);

        builder.HasIndex(session => new { session.CashDrawerId, session.Status });
        builder.HasIndex(session => new { session.CashDrawerId, session.OpenedAt });

        builder.HasMany(session => session.Movements)
            .WithOne()
            .HasForeignKey(movement => movement.CashSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(session => session.Movements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

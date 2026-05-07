using eiti.Domain.Cash;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CashDrawerUserAssignmentConfiguration : IEntityTypeConfiguration<CashDrawerUserAssignment>
{
    public void Configure(EntityTypeBuilder<CashDrawerUserAssignment> builder)
    {
        builder.ToTable("CashDrawerUserAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id)
            .IsRequired();

        builder.Property(assignment => assignment.CashDrawerId)
            .HasConversion(id => id.Value, value => new CashDrawerId(value))
            .IsRequired();

        builder.Property(assignment => assignment.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(assignment => assignment.AssignedAt)
            .IsRequired();

        builder.HasIndex(assignment => assignment.CashDrawerId);
        builder.HasIndex(assignment => assignment.UserId).IsUnique();

        builder.HasOne<CashDrawer>()
            .WithMany()
            .HasForeignKey(assignment => assignment.CashDrawerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

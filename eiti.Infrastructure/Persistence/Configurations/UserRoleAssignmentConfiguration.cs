using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(role => new { role.UserId, role.RoleCode });

        builder.Property(role => role.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(role => role.RoleCode)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(role => role.AssignedAt).IsRequired();
    }
}

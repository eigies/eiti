using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class AccessProfilePermissionConfiguration : IEntityTypeConfiguration<AccessProfilePermission>
{
    public void Configure(EntityTypeBuilder<AccessProfilePermission> builder)
    {
        builder.ToTable("AccessProfilePermissions");

        builder.HasKey(permission => new { permission.AccessProfileId, permission.PermissionCode });

        builder.Property(permission => permission.AccessProfileId)
            .HasConversion(
                id => id.Value,
                value => new AccessProfileId(value))
            .IsRequired();

        builder.Property(permission => permission.PermissionCode)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(permission => permission.AssignedAt).IsRequired();
    }
}

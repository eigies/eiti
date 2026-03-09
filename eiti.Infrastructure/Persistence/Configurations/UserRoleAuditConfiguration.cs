using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class UserRoleAuditConfiguration : IEntityTypeConfiguration<UserRoleAudit>
{
    public void Configure(EntityTypeBuilder<UserRoleAudit> builder)
    {
        builder.ToTable("UserRoleAudits");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .HasConversion(
                id => id.Value,
                value => new UserRoleAuditId(value))
            .IsRequired();

        builder.Property(audit => audit.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(audit => audit.TargetUserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(audit => audit.ChangedByUserId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new UserId(value.Value) : null)
            .IsRequired(false);

        builder.Property(audit => audit.PreviousRolesCsv)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(audit => audit.NewRolesCsv)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(audit => audit.ChangedAt).IsRequired();
    }
}

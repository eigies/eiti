using eiti.Domain.Audit;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .HasConversion(
                id => id.Value,
                value => new AuditLogId(value))
            .IsRequired();

        builder.Property(audit => audit.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(audit => audit.UserId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new UserId(value.Value) : null)
            .IsRequired(false);

        builder.Property(audit => audit.ActionType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(audit => audit.Succeeded)
            .IsRequired();

        builder.Property(audit => audit.ErrorCode)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(audit => audit.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(audit => audit.Timestamp).IsRequired();

        builder.HasIndex(audit => new { audit.CompanyId, audit.Timestamp });
        builder.HasIndex(audit => new { audit.CompanyId, audit.UserId, audit.Timestamp });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(audit => audit.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

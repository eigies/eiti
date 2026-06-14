using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class AccessProfileConfiguration : IEntityTypeConfiguration<AccessProfile>
{
    public void Configure(EntityTypeBuilder<AccessProfile> builder)
    {
        builder.ToTable("AccessProfiles");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(
                id => id.Value,
                value => new AccessProfileId(value))
            .IsRequired();

        builder.Property(profile => profile.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(profile => profile.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(profile => profile.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(profile => profile.SystemKey)
            .HasMaxLength(60)
            .IsRequired(false);

        builder.Property(profile => profile.IsSystem).IsRequired();
        builder.Property(profile => profile.CreatedAt).IsRequired();
        builder.Property(profile => profile.UpdatedAt).IsRequired();

        builder.HasIndex(profile => new { profile.CompanyId, profile.Name }).IsUnique();
        builder.HasIndex(profile => new { profile.CompanyId, profile.SystemKey }).IsUnique().HasFilter("\"SystemKey\" IS NOT NULL");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(profile => profile.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(profile => profile.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.AccessProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

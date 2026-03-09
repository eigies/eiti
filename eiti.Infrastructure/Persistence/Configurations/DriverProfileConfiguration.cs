using eiti.Domain.Companies;
using eiti.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles");
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.EmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.LicenseNumber).HasMaxLength(60).IsRequired();
        builder.Property(x => x.LicenseCategory).HasMaxLength(40).IsRequired(false);
        builder.Property(x => x.EmergencyContactName).HasMaxLength(120).IsRequired(false);
        builder.Property(x => x.EmergencyContactPhone).HasMaxLength(40).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => new { x.CompanyId, x.LicenseNumber }).IsUnique();
    }
}

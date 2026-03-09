using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new VehicleId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.BranchId).HasConversion(id => id!.Value, value => new BranchId(value)).IsRequired(false);
        builder.Property(x => x.AssignedDriverEmployeeId).HasConversion(id => id!.Value, value => new EmployeeId(value)).IsRequired(false);
        builder.Property(x => x.Plate).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(120).IsRequired(false);
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => new { x.CompanyId, x.Plate }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.AssignedDriverEmployeeId, x.IsActive });
    }
}

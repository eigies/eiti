using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Fleet;
using eiti.Domain.Users;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class FleetLogConfiguration : IEntityTypeConfiguration<FleetLog>
{
    public void Configure(EntityTypeBuilder<FleetLog> builder)
    {
        builder.ToTable("FleetLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new FleetLogId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.VehicleId).HasConversion(id => id.Value, value => new VehicleId(value)).IsRequired();
        builder.Property(x => x.PerformedByEmployeeId).HasConversion(id => id!.Value, value => new EmployeeId(value)).IsRequired(false);
        builder.Property(x => x.CreatedByUserId).HasConversion(id => id.Value, value => new UserId(value)).IsRequired();
        builder.Property(x => x.MaintenanceType).HasMaxLength(80).IsRequired(false);
        builder.Property(x => x.Description).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.VehicleId, x.OccurredAt });
        builder.HasIndex(x => new { x.CompanyId, x.Type, x.OccurredAt });
    }
}

using eiti.Domain.Companies;
using eiti.Domain.Employees;
using eiti.Domain.Sales;
using eiti.Domain.Transport;
using eiti.Domain.Users;
using eiti.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class SaleTransportAssignmentConfiguration : IEntityTypeConfiguration<SaleTransportAssignment>
{
    public void Configure(EntityTypeBuilder<SaleTransportAssignment> builder)
    {
        builder.ToTable("SaleTransportAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new SaleTransportAssignmentId(value)).IsRequired();
        builder.Property(x => x.SaleId).HasConversion(id => id.Value, value => new SaleId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.DriverEmployeeId).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.VehicleId).HasConversion(id => id.Value, value => new VehicleId(value)).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasConversion(id => id.Value, value => new UserId(value)).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.AssignedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => x.SaleId).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.DriverEmployeeId, x.Status });
        builder.HasIndex(x => new { x.CompanyId, x.VehicleId, x.Status });
    }
}

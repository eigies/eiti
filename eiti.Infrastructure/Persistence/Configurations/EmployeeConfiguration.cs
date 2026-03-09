using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new EmployeeId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.BranchId).HasConversion(id => id!.Value, value => new BranchId(value)).IsRequired(false);
        builder.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(40).IsRequired(false);
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired(false);
        builder.Property(x => x.Email).HasMaxLength(160).IsRequired(false);
        builder.Property(x => x.EmployeeRole).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeRole, x.IsActive });
        builder.HasIndex(x => new { x.CompanyId, x.LastName, x.FirstName });
    }
}

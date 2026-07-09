using eiti.Domain.Companies;
using eiti.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class PayrollDeductionConceptConfiguration : IEntityTypeConfiguration<PayrollDeductionConcept>
{
    public void Configure(EntityTypeBuilder<PayrollDeductionConcept> builder)
    {
        builder.ToTable("PayrollDeductionConcepts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new PayrollDeductionConceptId(value)).IsRequired();
        builder.Property(x => x.CompanyId).HasConversion(id => id.Value, value => new CompanyId(value)).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Percentage).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CompanyId, x.IsActive });
    }
}

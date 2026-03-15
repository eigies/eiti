using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Id)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(company => company.Name)
            .HasConversion(
                name => name.Value,
                value => CompanyName.Create(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(company => company.PrimaryDomain)
            .HasConversion(
                domain => domain.Value,
                value => CompanyDomain.Create(value))
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(company => company.IsWhatsAppEnabled)
            .IsRequired();

        builder.Property(company => company.WhatsAppSenderPhone)
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(company => company.DefaultNoDeliverySurcharge)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(company => company.CreatedAt).IsRequired();

        builder.HasIndex(company => company.PrimaryDomain).IsUnique();
    }
}

using eiti.Domain.Branches;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class FiscalPointOfSaleConfiguration : IEntityTypeConfiguration<FiscalPointOfSale>
{
    public void Configure(EntityTypeBuilder<FiscalPointOfSale> builder)
    {
        builder.ToTable("FiscalPointsOfSale");

        builder.HasKey(pointOfSale => pointOfSale.Id);

        builder.Property(pointOfSale => pointOfSale.Id)
            .HasConversion(id => id.Value, value => new FiscalPointOfSaleId(value))
            .IsRequired();

        builder.Property(pointOfSale => pointOfSale.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(pointOfSale => pointOfSale.Number).IsRequired();
        builder.Property(pointOfSale => pointOfSale.CreatedAt).IsRequired();
        builder.Property(pointOfSale => pointOfSale.UpdatedAt).IsRequired(false);

        // El mismo número no puede estar en dos sucursales de la empresa.
        builder.HasIndex(pointOfSale => new { pointOfSale.CompanyId, pointOfSale.Number }).IsUnique();
    }
}

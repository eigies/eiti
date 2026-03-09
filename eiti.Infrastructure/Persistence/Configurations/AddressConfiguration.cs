using eiti.Domain.Addresses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(address => address.Id);

        builder.Property(address => address.Id)
            .HasConversion(
                id => id.Value,
                value => new AddressId(value))
            .IsRequired();

        builder.Property(address => address.Street).HasMaxLength(120).IsRequired();
        builder.Property(address => address.StreetNumber).HasMaxLength(20).IsRequired();
        builder.Property(address => address.Floor).HasMaxLength(20).IsRequired(false);
        builder.Property(address => address.Apartment).HasMaxLength(20).IsRequired(false);
        builder.Property(address => address.PostalCode).HasMaxLength(20).IsRequired();
        builder.Property(address => address.City).HasMaxLength(100).IsRequired();
        builder.Property(address => address.StateOrProvince).HasMaxLength(100).IsRequired();
        builder.Property(address => address.Country).HasMaxLength(100).IsRequired();
        builder.Property(address => address.Reference).HasMaxLength(200).IsRequired(false);
        builder.Property(address => address.CreatedAt).IsRequired();
        builder.Property(address => address.UpdatedAt).IsRequired(false);
    }
}

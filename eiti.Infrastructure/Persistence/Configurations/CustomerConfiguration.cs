using eiti.Domain.Addresses;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .HasConversion(
                id => id.Value,
                value => new CustomerId(value))
            .IsRequired();

        builder.Property(customer => customer.Name)
            .HasMaxLength(201)
            .IsRequired();

        builder.Property(customer => customer.CompanyId)
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(customer => customer.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(customer => customer.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(customer => customer.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(customer => customer.Phone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(customer => customer.DocumentType)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(customer => customer.DocumentNumber)
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(customer => customer.TaxId)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(customer => customer.AddressId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new AddressId(value.Value) : null)
            .IsRequired(false);

        builder.HasIndex(customer => new { customer.CompanyId, customer.Email }).IsUnique();
        builder.HasIndex(customer => new { customer.CompanyId, customer.DocumentType, customer.DocumentNumber })
            .IsUnique()
            .HasFilter("[DocumentType] IS NOT NULL AND [DocumentNumber] IS NOT NULL");
        builder.HasIndex(customer => new { customer.CompanyId, customer.TaxId })
            .IsUnique()
            .HasFilter("[TaxId] IS NOT NULL");
        builder.HasIndex(customer => new { customer.CompanyId, customer.Name });

        builder.Property(customer => customer.CreatedAt).IsRequired();
        builder.Property(customer => customer.UpdatedAt).IsRequired(false);

        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(customer => customer.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(customer => customer.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

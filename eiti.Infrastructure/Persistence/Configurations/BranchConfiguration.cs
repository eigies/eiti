using eiti.Domain.Branches;
using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Id)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(branch => branch.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(branch => branch.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(branch => branch.Code)
            .HasMaxLength(40)
            .IsRequired(false);

        builder.Property(branch => branch.Address)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(branch => branch.CreatedAt).IsRequired();
        builder.Property(branch => branch.UpdatedAt).IsRequired(false);

        builder.HasIndex(branch => new { branch.CompanyId, branch.Name }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(branch => branch.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

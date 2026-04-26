using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CashDrawerConfiguration : IEntityTypeConfiguration<CashDrawer>
{
    public void Configure(EntityTypeBuilder<CashDrawer> builder)
    {
        builder.ToTable("CashDrawers");

        builder.HasKey(drawer => drawer.Id);

        builder.Property(drawer => drawer.Id)
            .HasConversion(id => id.Value, value => new CashDrawerId(value))
            .IsRequired();

        builder.Property(drawer => drawer.CompanyId)
            .HasConversion(id => id.Value, value => new CompanyId(value))
            .IsRequired();

        builder.Property(drawer => drawer.BranchId)
            .HasConversion(id => id.Value, value => new BranchId(value))
            .IsRequired();

        builder.Property(drawer => drawer.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(drawer => drawer.IsActive).IsRequired();
        builder.Property(drawer => drawer.CreatedAt).IsRequired();
        builder.Property(drawer => drawer.UpdatedAt).IsRequired(false);

        builder.Property(drawer => drawer.AssignedUserId)
            .HasConversion(id => id!.Value, value => new UserId(value))
            .IsRequired(false);

        builder.HasIndex(drawer => drawer.AssignedUserId);

        builder.HasIndex(drawer => new { drawer.BranchId, drawer.Name }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(drawer => drawer.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(drawer => drawer.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

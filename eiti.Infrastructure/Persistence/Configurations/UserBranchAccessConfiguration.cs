using eiti.Domain.Branches;
using eiti.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class UserBranchAccessConfiguration : IEntityTypeConfiguration<UserBranchAccess>
{
    public void Configure(EntityTypeBuilder<UserBranchAccess> builder)
    {
        builder.ToTable("UserBranchAccesses");

        builder.HasKey(access => access.Id);

        builder.Property(access => access.Id).ValueGeneratedNever().IsRequired();

        builder.Property(access => access.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(access => access.BranchId)
            .HasConversion(
                id => id.Value,
                value => new BranchId(value))
            .IsRequired();

        builder.HasIndex(access => new { access.UserId, access.BranchId }).IsUnique();

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(access => access.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using eiti.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eiti.Infrastructure.Persistence.Configurations;

public sealed class CompanyOnboardingConfiguration : IEntityTypeConfiguration<CompanyOnboarding>
{
    public void Configure(EntityTypeBuilder<CompanyOnboarding> builder)
    {
        builder.ToTable("CompanyOnboarding");

        builder.HasKey(onboarding => onboarding.Id);

        builder.Property(onboarding => onboarding.Id)
            .HasColumnName("CompanyId")
            .HasConversion(
                id => id.Value,
                value => new CompanyId(value))
            .IsRequired();

        builder.Property(onboarding => onboarding.HasCreatedBranch).IsRequired();
        builder.Property(onboarding => onboarding.HasCreatedCashDrawer).IsRequired();
        builder.Property(onboarding => onboarding.HasCompletedInitialCashOpen).IsRequired();
        builder.Property(onboarding => onboarding.HasCreatedProduct).IsRequired();
        builder.Property(onboarding => onboarding.HasLoadedInitialStock).IsRequired();
        builder.Property(onboarding => onboarding.CompletedAt);
        builder.Property(onboarding => onboarding.UpdatedAt).IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InsuranceClaims.Domain.PolicyManagement;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the PolicyCoverage entity.
/// </summary>
public class PolicyCoverageConfiguration : IEntityTypeConfiguration<PolicyCoverage>
{
    public void Configure(EntityTypeBuilder<PolicyCoverage> builder)
    {
        builder.ToTable("PolicyCoverages");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PolicyId)
            .IsRequired();

        builder.HasIndex(c => c.PolicyId);

        builder.Property(c => c.CoverageType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.CoverageLimit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.DeductibleAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.PercentageOfCoverage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();
    }
}

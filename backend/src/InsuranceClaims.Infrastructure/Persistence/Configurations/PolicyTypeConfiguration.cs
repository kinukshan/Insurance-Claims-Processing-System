using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InsuranceClaims.Domain.PolicyManagement;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the PolicyType entity.
/// </summary>
public class PolicyTypeConfiguration : IEntityTypeConfiguration<PolicyType>
{
    public void Configure(EntityTypeBuilder<PolicyType> builder)
    {
        builder.ToTable("PolicyTypes");

        builder.HasKey(pt => pt.Id);

        builder.Property(pt => pt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(pt => pt.Name)
            .IsUnique();

        builder.Property(pt => pt.Description)
            .HasMaxLength(500);

        builder.Property(pt => pt.BasePremiumRate)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(pt => pt.DefaultCoverageLimit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(pt => pt.DefaultDeductible)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(pt => pt.RiskMultiplier)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(pt => pt.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(pt => pt.CreatedAt)
            .IsRequired();

        builder.Property(pt => pt.UpdatedAt)
            .IsRequired();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Policy entity.
/// </summary>
public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PolicyNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(p => p.PolicyNumber)
            .IsUnique();

        builder.Property(p => p.PolicyholderId)
            .IsRequired();

        builder.HasIndex(p => p.PolicyholderId);

        builder.Property(p => p.PolicyTypeId)
            .IsRequired();

        builder.Property(p => p.CoverageLimit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Premium)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Deductible)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.StartDate)
            .IsRequired();

        builder.Property(p => p.ExpiryDate)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.Status);

        builder.Property(p => p.RenewalStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Exclusions)
            .HasMaxLength(2000);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(p => p.PolicyType)
            .WithMany(pt => pt.Policies)
            .HasForeignKey(p => p.PolicyTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Coverages)
            .WithOne(c => c.Policy)
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using InsuranceClaims.Domain.ClaimsManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Claim entity.
/// Defines PKs, FKs, indexes, constraints, and decimal precision.
/// </summary>
public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.ClaimNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.IncidentLocation)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.ClaimedAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.ClaimType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.IncidentDate)
            .IsRequired();

        builder.Property(c => c.SubmittedAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.ClaimNumber)
            .IsUnique();

        builder.HasIndex(c => c.PolicyId);

        builder.HasIndex(c => c.PolicyHolderId);

        builder.HasIndex(c => c.Status);

        builder.HasIndex(c => c.SubmittedAt);

        // Relationships
        // Claim -> Policy (FK contract — Policy entity owned by Kaushikesh)
        builder.HasOne(c => c.Policy)
            .WithMany()
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Claim -> ClaimDocuments (one-to-many)
        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Claim)
            .HasForeignKey(d => d.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

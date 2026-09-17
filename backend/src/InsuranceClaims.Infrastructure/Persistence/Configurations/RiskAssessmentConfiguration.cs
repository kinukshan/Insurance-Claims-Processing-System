using InsuranceClaims.Domain.ClaimsManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the RiskAssessment entity.
/// </summary>
public class RiskAssessmentConfiguration : IEntityTypeConfiguration<Domain.RiskAssessment.RiskAssessment>
{
    public void Configure(EntityTypeBuilder<Domain.RiskAssessment.RiskAssessment> builder)
    {
        builder.ToTable("risk_assessments");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(r => r.RiskScore)
            .HasColumnName("risk_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.RiskLevel)
            .HasColumnName("risk_level")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Recommendation)
            .HasColumnName("recommendation")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.AssessorType)
            .HasColumnName("assessor_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.AssessmentTimestamp)
            .HasColumnName("assessment_timestamp")
            .IsRequired();

        builder.Property(r => r.Summary)
            .HasColumnName("summary")
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Check Constraint ────────────────────────────────────
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_risk_assessments_risk_score",
            "risk_score >= 0 AND risk_score <= 100"));

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(r => r.ClaimId)
            .HasDatabaseName("IX_risk_assessments_claim_id");

        // ── Relationships ───────────────────────────────────────
        // RiskAssessment → Claim (many-to-one, navigation-less)
        builder.HasOne<Claim>()
            .WithMany()
            .HasForeignKey(r => r.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);

        // RiskAssessment → FraudFlags (one-to-many)
        builder.HasMany(r => r.FraudFlags)
            .WithOne(f => f.RiskAssessment)
            .HasForeignKey(f => f.RiskAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // RiskAssessment → FraudCase (one-to-one, optional)
        builder.HasOne(r => r.FraudCase)
            .WithOne(fc => fc.RiskAssessment)
            .HasForeignKey<Domain.RiskAssessment.FraudCase>(fc => fc.RiskAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

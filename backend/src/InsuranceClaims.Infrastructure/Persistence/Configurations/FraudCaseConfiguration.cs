using InsuranceClaims.Domain.RiskAssessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the FraudCase entity.
/// </summary>
public class FraudCaseConfiguration : IEntityTypeConfiguration<FraudCase>
{
    public void Configure(EntityTypeBuilder<FraudCase> builder)
    {
        builder.ToTable("fraud_cases");

        builder.HasKey(fc => fc.Id);

        builder.Property(fc => fc.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(fc => fc.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(fc => fc.RiskAssessmentId)
            .HasColumnName("risk_assessment_id")
            .IsRequired();

        builder.Property(fc => fc.PolicyHolderId)
            .HasColumnName("policy_holder_id")
            .IsRequired();

        builder.Property(fc => fc.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(fc => fc.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(fc => fc.AssignedReviewer)
            .HasColumnName("assigned_reviewer")
            .HasMaxLength(200);

        builder.Property(fc => fc.Notes)
            .HasColumnName("notes")
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(fc => fc.Resolution)
            .HasColumnName("resolution")
            .HasMaxLength(2000);

        builder.Property(fc => fc.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(fc => fc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(fc => fc.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(fc => fc.ClaimId)
            .HasDatabaseName("IX_fraud_cases_claim_id");

        builder.HasIndex(fc => fc.PolicyHolderId)
            .HasDatabaseName("IX_fraud_cases_policy_holder_id");

        builder.HasIndex(fc => fc.Status)
            .HasDatabaseName("IX_fraud_cases_status");

        builder.HasIndex(fc => fc.RiskAssessmentId)
            .IsUnique()
            .HasDatabaseName("IX_fraud_cases_risk_assessment_id");

        // NOTE: FK to Claim (FraudCase.ClaimId → Claim.Id)
        // and FK to PolicyHolder will be configured during migration by Kinukshan.
    }
}

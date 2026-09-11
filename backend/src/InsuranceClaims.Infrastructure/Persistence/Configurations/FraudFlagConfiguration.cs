using InsuranceClaims.Domain.RiskAssessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the FraudFlag entity.
/// </summary>
public class FraudFlagConfiguration : IEntityTypeConfiguration<FraudFlag>
{
    public void Configure(EntityTypeBuilder<FraudFlag> builder)
    {
        builder.ToTable("fraud_flags");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(f => f.RiskAssessmentId)
            .HasColumnName("risk_assessment_id")
            .IsRequired();

        builder.Property(f => f.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(f => f.FlagType)
            .HasColumnName("flag_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .HasDefaultValue(string.Empty);

        builder.Property(f => f.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(f => f.IsResolved)
            .HasColumnName("is_resolved")
            .HasDefaultValue(false);

        builder.Property(f => f.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(f => f.ResolvedBy)
            .HasColumnName("resolved_by")
            .HasMaxLength(200);

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(f => f.RiskAssessmentId)
            .HasDatabaseName("IX_fraud_flags_risk_assessment_id");

        builder.HasIndex(f => f.ClaimId)
            .HasDatabaseName("IX_fraud_flags_claim_id");

        builder.HasIndex(f => f.IsResolved)
            .HasDatabaseName("IX_fraud_flags_is_resolved");

        // NOTE: FK to Claim (FraudFlag.ClaimId → Claim.Id)
        // will be configured during migration by Kinukshan.
    }
}

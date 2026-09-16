using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the PayoutApproval entity.
/// No decimal-precision configuration — PayoutApproval has no decimal properties.
/// </summary>
public class PayoutApprovalConfiguration : IEntityTypeConfiguration<PayoutApproval>
{
    public void Configure(EntityTypeBuilder<PayoutApproval> builder)
    {
        builder.ToTable("PayoutApprovals");

        // Primary key
        builder.HasKey(a => a.Id);

        // ── FK to Payout — configured in PayoutConfiguration via HasMany ──

        // ── Enum conversion ──────────────────────────────────────────
        builder.Property(a => a.Decision)
            .HasConversion<int>();

        // ── String constraints ───────────────────────────────────────
        builder.Property(a => a.ReviewerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Comments)
            .HasMaxLength(2000);

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(a => a.PayoutId);
        builder.HasIndex(a => a.ReviewerId);
        builder.HasIndex(a => a.DecisionTimestamp);
    }
}

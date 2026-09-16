using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Payout entity.
/// </summary>
public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");

        // Primary key
        builder.HasKey(p => p.Id);

        // ── Foreign keys ─────────────────────────────────────────────
        // Payout.ClaimId → Claim.Id
        // Claim entity is owned by Arulkumaran — we only configure the FK here.
        builder.HasOne(p => p.Claim)
            .WithMany()
            .HasForeignKey(p => p.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Decimal precision for financial columns ──────────────────
        builder.Property(p => p.ApprovedClaimAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.CoverageLimit)
            .HasPrecision(18, 2);

        builder.Property(p => p.Deductible)
            .HasPrecision(18, 2);

        builder.Property(p => p.ProposedPayout)
            .HasPrecision(18, 2);

        builder.Property(p => p.FinalPayout)
            .HasPrecision(18, 2);

        // ── Status ───────────────────────────────────────────────────
        builder.Property(p => p.Status)
            .HasConversion<int>();

        // ── String constraints ───────────────────────────────────────
        builder.Property(p => p.ApprovedBy)
            .HasMaxLength(200);

        builder.Property(p => p.PaymentReference)
            .HasMaxLength(100);

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(p => p.ClaimId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);

        // ── Navigation: Approvals ────────────────────────────────────
        builder.HasMany(p => p.Approvals)
            .WithOne(a => a.Payout)
            .HasForeignKey(a => a.PayoutId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

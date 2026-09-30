using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the PaymentTransaction entity.
/// </summary>
public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        // Primary key
        builder.HasKey(t => t.Id);

        // ── Foreign keys ─────────────────────────────────────────────
        builder.HasOne(t => t.Payout)
            .WithMany(p => p.PaymentTransactions)
            .HasForeignKey(t => t.PayoutId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Decimal precision ────────────────────────────────────────
        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        // ── Enum conversion ──────────────────────────────────────────
        builder.Property(t => t.Status)
            .HasConversion<int>();

        // ── String constraints ───────────────────────────────────────
        builder.Property(t => t.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.ProviderTransactionId)
            .HasMaxLength(200);

        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.FailureCode)
            .HasMaxLength(100);

        builder.Property(t => t.FailureMessage)
            .HasMaxLength(2000);

        builder.Property(t => t.ProviderEventId)
            .HasMaxLength(200);

        builder.Property(t => t.ProviderBatchId)
            .HasMaxLength(200);

        builder.Property(t => t.ProviderItemId)
            .HasMaxLength(200);

        builder.Property(t => t.SenderBatchId)
            .HasMaxLength(200);

        builder.Property(t => t.SenderItemId)
            .HasMaxLength(200);

        builder.Property(t => t.Recipient)
            .HasMaxLength(255);

        builder.Property(t => t.ProviderStatusRaw)
            .HasMaxLength(100);

        // ── Indexes ──────────────────────────────────────────────────
        // CRITICAL: Unique idempotency key prevents duplicate payment creation
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(t => t.PayoutId);
        builder.HasIndex(t => t.ProviderTransactionId);
        builder.HasIndex(t => t.ProviderBatchId);
        builder.HasIndex(t => t.ProviderItemId);
        builder.HasIndex(t => t.SenderBatchId);
        builder.HasIndex(t => t.SenderItemId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.CreatedAt);
    }
}

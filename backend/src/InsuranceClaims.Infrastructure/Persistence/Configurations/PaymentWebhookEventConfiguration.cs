using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for PaymentWebhookEvent entity.
/// Enforces unique constraint on (Provider, ProviderEventId) for replay safety.
/// </summary>
public class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ProviderEventId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.ResourceType)
            .HasMaxLength(100);

        builder.Property(e => e.Summary)
            .HasMaxLength(500);

        builder.Property(e => e.ProcessingStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.FailureReason)
            .HasMaxLength(1000);

        // ── Foreign key ──────────────────────────────────────────────
        builder.HasOne(e => e.PaymentTransaction)
            .WithMany(t => t.WebhookEvents)
            .HasForeignKey(e => e.PaymentTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Indexes ──────────────────────────────────────────────────
        // Replay safety: Provider + ProviderEventId must be unique
        builder.HasIndex(e => new { e.Provider, e.ProviderEventId })
            .IsUnique();

        builder.HasIndex(e => e.PaymentTransactionId);
        builder.HasIndex(e => e.ReceivedAt);
        builder.HasIndex(e => e.EventType);
    }
}

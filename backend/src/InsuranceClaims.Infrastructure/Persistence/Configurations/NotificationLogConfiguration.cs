using InsuranceClaims.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the NotificationLog entity.
/// </summary>
public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId)
            .IsRequired();

        builder.Property(n => n.ClaimId);

        builder.Property(n => n.PayoutId);

        builder.Property(n => n.NotificationKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(n => n.NotificationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.Channel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(n => n.Recipient)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(n => n.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Body)
            .IsRequired();

        builder.Property(n => n.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(NotificationStatus.Pending);

        builder.Property(n => n.Success)
            .IsRequired();

        builder.Property(n => n.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(n => n.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(n => n.ProviderMessageId)
            .HasMaxLength(256);

        builder.Property(n => n.SentAt)
            .IsRequired(false);

        // Unique index for idempotency checks: NotificationKey
        builder.HasIndex(n => n.NotificationKey)
            .IsUnique()
            .HasDatabaseName("IX_NotificationLogs_NotificationKey");

        // Index for querying by claim
        builder.HasIndex(n => n.ClaimId)
            .HasDatabaseName("IX_NotificationLogs_ClaimId");

        // Index for querying by payout
        builder.HasIndex(n => n.PayoutId)
            .HasDatabaseName("IX_NotificationLogs_PayoutId");

        // Index for querying by user
        builder.HasIndex(n => n.UserId)
            .HasDatabaseName("IX_NotificationLogs_UserId");
    }
}

using InsuranceClaims.Domain.AgentWorkflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ApprovalDecision entity.
/// </summary>
public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("approval_decisions");

        // Primary key
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.WorkflowId)
            .HasColumnName("workflow_id")
            .IsRequired();

        builder.Property(a => a.ReviewerId)
            .HasColumnName("reviewer_id")
            .IsRequired();

        builder.Property(a => a.Decision)
            .HasColumnName("decision")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Comments)
            .HasColumnName("comments")
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(a => a.WorkflowId)
            .HasDatabaseName("IX_approval_decisions_workflow_id");

        builder.HasIndex(a => a.ReviewerId)
            .HasDatabaseName("IX_approval_decisions_reviewer_id");

        // ── Relationships ───────────────────────────────────────
        // ApprovalDecision → AgentWorkflow (many-to-one, navigation-less)
        builder.HasOne<AgentWorkflow>()
            .WithMany()
            .HasForeignKey(a => a.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

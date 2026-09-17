using InsuranceClaims.Domain.AgentWorkflows;
using InsuranceClaims.Domain.ClaimsManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the AgentWorkflow entity.
/// </summary>
public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("agent_workflows");

        // Primary key
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(w => w.ClaimId)
            .HasColumnName("claim_id")
            .IsRequired();

        builder.Property(w => w.Objective)
            .HasColumnName("objective")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(w => w.Plan)
            .HasColumnName("plan")
            .HasColumnType("text");

        builder.Property(w => w.FinalOutcome)
            .HasColumnName("final_outcome")
            .HasColumnType("text");

        builder.Property(w => w.ExecutionSummary)
            .HasColumnName("execution_summary")
            .HasColumnType("text");

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(w => w.ClaimId)
            .HasDatabaseName("IX_agent_workflows_claim_id");

        builder.HasIndex(w => w.Status)
            .HasDatabaseName("IX_agent_workflows_status");

        // ── Relationships ───────────────────────────────────────
        // AgentWorkflow → Claim (many-to-one, navigation-less)
        builder.HasOne<Claim>()
            .WithMany()
            .HasForeignKey(w => w.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

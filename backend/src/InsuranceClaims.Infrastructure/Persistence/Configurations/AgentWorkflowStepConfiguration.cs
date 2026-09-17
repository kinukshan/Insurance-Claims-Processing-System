using InsuranceClaims.Domain.AgentWorkflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the AgentWorkflowStep entity.
/// </summary>
public class AgentWorkflowStepConfiguration : IEntityTypeConfiguration<AgentWorkflowStep>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowStep> builder)
    {
        builder.ToTable("agent_workflow_steps");

        // Primary key
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.WorkflowId)
            .HasColumnName("workflow_id")
            .IsRequired();

        builder.Property(s => s.AgentName)
            .HasColumnName("agent_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.StepName)
            .HasColumnName("step_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.Input)
            .HasColumnName("input")
            .HasColumnType("text");

        builder.Property(s => s.Output)
            .HasColumnName("output")
            .HasColumnType("text");

        builder.Property(s => s.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(s => s.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(s => s.WorkflowId)
            .HasDatabaseName("IX_agent_workflow_steps_workflow_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_agent_workflow_steps_status");

        // ── Relationships ───────────────────────────────────────
        // AgentWorkflowStep → AgentWorkflow (many-to-one, navigation-less)
        builder.HasOne<AgentWorkflow>()
            .WithMany()
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

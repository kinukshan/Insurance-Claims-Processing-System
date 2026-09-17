using InsuranceClaims.Domain.AgentWorkflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceClaims.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the AgentExecutionLog entity.
/// </summary>
public class AgentExecutionLogConfiguration : IEntityTypeConfiguration<AgentExecutionLog>
{
    public void Configure(EntityTypeBuilder<AgentExecutionLog> builder)
    {
        builder.ToTable("agent_execution_logs");

        // Primary key
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(l => l.WorkflowId)
            .HasColumnName("workflow_id")
            .IsRequired();

        builder.Property(l => l.StepId)
            .HasColumnName("step_id");

        builder.Property(l => l.LogLevel)
            .HasColumnName("log_level")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Message)
            .HasColumnName("message")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // ── Indexes ─────────────────────────────────────────────
        builder.HasIndex(l => l.WorkflowId)
            .HasDatabaseName("IX_agent_execution_logs_workflow_id");

        builder.HasIndex(l => l.StepId)
            .HasDatabaseName("IX_agent_execution_logs_step_id");

        // ── Relationships ───────────────────────────────────────
        // AgentExecutionLog → AgentWorkflow (many-to-one, navigation-less)
        builder.HasOne<AgentWorkflow>()
            .WithMany()
            .HasForeignKey(l => l.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // AgentExecutionLog → AgentWorkflowStep (many-to-one, optional, navigation-less)
        builder.HasOne<AgentWorkflowStep>()
            .WithMany()
            .HasForeignKey(l => l.StepId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

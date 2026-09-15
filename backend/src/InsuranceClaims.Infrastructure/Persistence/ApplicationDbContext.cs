using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.AgentWorkflows;
using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the Insurance Claims system.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Users
    public DbSet<User> Users => Set<User>();

    // Policy Management
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyType> PolicyTypes => Set<PolicyType>();
    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();

    // Claims Management
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    // Risk Assessment
    public DbSet<Domain.RiskAssessment.RiskAssessment> RiskAssessments => Set<Domain.RiskAssessment.RiskAssessment>();
    public DbSet<FraudFlag> FraudFlags => Set<FraudFlag>();
    public DbSet<FraudCase> FraudCases => Set<FraudCase>();

    // Payout Processing
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutApproval> PayoutApprovals => Set<PayoutApproval>();

    // Agent Workflows
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();
    public DbSet<AgentWorkflowStep> AgentWorkflowSteps => Set<AgentWorkflowStep>();
    public DbSet<AgentExecutionLog> AgentExecutionLogs => Set<AgentExecutionLog>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Claims Management configurations
        modelBuilder.ApplyConfiguration(new Configurations.ClaimConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ClaimDocumentConfiguration());
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

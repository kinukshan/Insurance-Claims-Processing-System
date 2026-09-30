using InsuranceClaims.Domain.AgentWorkflows;
using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Repository contract for claim data access.
/// </summary>
public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(Guid id);
    Task<Claim?> GetByIdWithDocumentsAsync(Guid id);
    Task<List<Claim>> GetByPolicyHolderIdAsync(Guid policyHolderId);
    Task<List<Claim>> GetAllAsync(string? statusFilter = null, string? searchTerm = null, Guid? policyHolderId = null);
    Task<Claim> AddAsync(Claim claim);
    Task<Claim> UpdateAsync(Claim claim);
    Task DeleteAsync(Claim claim);
    Task DeleteDocumentAsync(ClaimDocument document);
    Task<bool> ExistsAsync(Guid id);
    Task<string> GenerateClaimNumberAsync();
    Task<AgentWorkflow?> GetWorkflowAttemptByIdempotencyKeyAsync(Guid claimId, string idempotencyKey);
    Task<AgentWorkflow> RecordWorkflowAttemptAsync(AgentWorkflow workflow);
}

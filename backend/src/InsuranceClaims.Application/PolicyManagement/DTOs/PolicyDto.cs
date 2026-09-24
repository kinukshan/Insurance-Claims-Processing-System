namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Response DTO for policy data.
/// </summary>
public class PolicyDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid PolicyholderId { get; set; }
    public Guid PolicyTypeId { get; set; }
    public string PolicyTypeName { get; set; } = string.Empty;
    public int InsuranceClass { get; set; }
    public string InsuranceClassCode { get; set; } = "General";
    public string InsuranceClassName { get; set; } = "General Insurance";
    public decimal CoverageLimit { get; set; }
    public decimal Premium { get; set; }
    public decimal Deductible { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RenewalStatus { get; set; } = string.Empty;
    public string? Exclusions { get; set; }
    public bool IsExpired { get; set; }
    public bool CanRenew { get; set; }
    public List<PolicyCoverageDto> Coverages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

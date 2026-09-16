namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Response DTO for policy coverage details.
/// </summary>
public class PolicyCoverageDto
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public string CoverageType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal DeductibleAmount { get; set; }
    public decimal PercentageOfCoverage { get; set; }
    public bool IsActive { get; set; }
}

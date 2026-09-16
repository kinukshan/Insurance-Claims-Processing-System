namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Result DTO for policy renewal operation.
/// </summary>
public class PolicyRenewalResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? OriginalPolicyId { get; set; }
    public Guid? RenewedPolicyId { get; set; }
    public string? RenewedPolicyNumber { get; set; }
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewExpiryDate { get; set; }
    public decimal? NewPremium { get; set; }
}

namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Result DTO for premium calculation.
/// </summary>
public class PremiumCalculationResultDto
{
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public decimal BasePremiumRate { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal RiskMultiplier { get; set; }
    public decimal DeductibleDiscount { get; set; }
    public decimal CalculatedPremium { get; set; }
    public string Breakdown { get; set; } = string.Empty;
}

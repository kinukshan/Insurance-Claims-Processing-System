using System.ComponentModel.DataAnnotations;

namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Input DTO for creating a new policy.
/// </summary>
public class CreatePolicyDto
{
    [Required]
    public Guid PolicyholderId { get; set; }

    [Required]
    public Guid PolicyTypeId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Coverage limit must be greater than zero.")]
    public decimal CoverageLimit { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Deductible cannot be negative.")]
    public decimal Deductible { get; set; }

    [Range(0, 100, ErrorMessage = "Deductible percentage must be between 0 and 100.")]
    public decimal? DeductiblePercentage { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    public string? Exclusions { get; set; }
}

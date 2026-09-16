using System.ComponentModel.DataAnnotations;

namespace InsuranceClaims.Application.PolicyManagement.DTOs;

/// <summary>
/// Input DTO for updating an existing policy.
/// Only permitted fields can be updated.
/// </summary>
public class UpdatePolicyDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Coverage limit must be greater than zero.")]
    public decimal? CoverageLimit { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Deductible cannot be negative.")]
    public decimal? Deductible { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Exclusions { get; set; }

    /// <summary>
    /// Optional status change (e.g., Active, Cancelled).
    /// Only certain transitions are valid.
    /// </summary>
    public string? Status { get; set; }
}

using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Request DTO for updating a fraud case.
/// </summary>
public class UpdateFraudCaseRequest
{
    /// <summary>New investigation status.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public FraudCaseStatus? Status { get; set; }

    /// <summary>Updated investigation notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Resolution description (required when closing).</summary>
    public string? Resolution { get; set; }

    /// <summary>Reassign to a different reviewer.</summary>
    public string? AssignedReviewer { get; set; }

    /// <summary>Updated priority.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public FraudCasePriority? Priority { get; set; }
}

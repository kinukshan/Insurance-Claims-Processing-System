using InsuranceClaims.Application.RiskAssessment.DTOs;

namespace InsuranceClaims.Application.RiskAssessment.Validators;

/// <summary>
/// Validates risk assessment request inputs.
/// </summary>
public static class AssessClaimRequestValidator
{
    /// <summary>
    /// Validates an assess claim request. Returns a list of validation errors.
    /// </summary>
    public static List<string> Validate(Guid claimId, AssessClaimRequest? request)
    {
        var errors = new List<string>();

        if (claimId == Guid.Empty)
        {
            errors.Add("ClaimId must be a valid non-empty GUID.");
        }

        if (request == null)
        {
            errors.Add("Request body is required.");
        }

        return errors;
    }

    /// <summary>
    /// Validates an escalation request.
    /// </summary>
    public static List<string> ValidateEscalation(Guid assessmentId, EscalateRequest? request)
    {
        var errors = new List<string>();

        if (assessmentId == Guid.Empty)
        {
            errors.Add("Assessment ID must be a valid non-empty GUID.");
        }

        if (request == null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("Escalation reason is required.");
        }

        return errors;
    }

    /// <summary>
    /// Validates a fraud case update request.
    /// </summary>
    public static List<string> ValidateUpdateFraudCase(Guid fraudCaseId, UpdateFraudCaseRequest? request)
    {
        var errors = new List<string>();

        if (fraudCaseId == Guid.Empty)
        {
            errors.Add("Fraud case ID must be a valid non-empty GUID.");
        }

        if (request == null)
        {
            errors.Add("Request body is required.");
            return errors;
        }

        // If closing, resolution is required
        if (request.Status is Domain.RiskAssessment.Enums.FraudCaseStatus.Resolved or
            Domain.RiskAssessment.Enums.FraudCaseStatus.Dismissed)
        {
            if (string.IsNullOrWhiteSpace(request.Resolution))
            {
                errors.Add("Resolution description is required when closing a fraud case.");
            }
        }

        return errors;
    }
}

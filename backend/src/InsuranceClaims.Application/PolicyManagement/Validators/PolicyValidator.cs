using InsuranceClaims.Application.Common;
using InsuranceClaims.Application.Common.Interfaces;
using InsuranceClaims.Application.PolicyManagement.DTOs;

namespace InsuranceClaims.Application.PolicyManagement.Validators;

/// <summary>
/// Validates policy data for creation and updates.
/// </summary>
public static class PolicyValidator
{
    /// <summary>
    /// Validates a create policy request.
    /// Returns a list of validation errors (empty if valid).
    /// </summary>
    public static List<string> ValidateCreate(CreatePolicyDto dto, IBusinessCalendar? calendar = null)
    {
        var errors = new List<string>();

        if (dto.PolicyholderId == Guid.Empty)
            errors.Add("PolicyholderId is required.");

        if (dto.PolicyTypeId == Guid.Empty)
            errors.Add("PolicyTypeId is required.");

        if (dto.CoverageLimit <= 0)
            errors.Add("Coverage limit must be greater than zero.");

        if (dto.Deductible < 0)
            errors.Add("Deductible cannot be negative.");

        if (dto.DeductiblePercentage.HasValue && (dto.DeductiblePercentage.Value < 0 || dto.DeductiblePercentage.Value > 100))
            errors.Add("Deductible percentage must be between 0 and 100.");

        if (dto.StartDate == default)
            errors.Add("Start date is required.");

        if (dto.ExpiryDate == default)
            errors.Add("Expiry date is required.");

        if (dto.StartDate != default)
        {
            var today = (calendar ?? BusinessCalendar.Default).Today;
            if (dto.StartDate.Date < today)
                errors.Add("Start date cannot be before today.");
        }

        if (dto.StartDate != default && dto.ExpiryDate != default && dto.ExpiryDate.Date <= dto.StartDate.Date)
        {
            errors.Add("Expiry date must be later than the start date. Start date must be before expiry date.");
        }

        if (dto.Exclusions != null && dto.Exclusions.Length > 2000)
            errors.Add("Exclusions text cannot exceed 2000 characters.");

        return errors;
    }

    /// <summary>
    /// Validates an update policy request.
    /// Returns a list of validation errors (empty if valid).
    /// </summary>
    public static List<string> ValidateUpdate(UpdatePolicyDto dto)
    {
        var errors = new List<string>();

        if (dto.CoverageLimit.HasValue && dto.CoverageLimit.Value <= 0)
            errors.Add("Coverage limit must be greater than zero.");

        if (dto.Deductible.HasValue && dto.Deductible.Value < 0)
            errors.Add("Deductible cannot be negative.");

        if (dto.DeductiblePercentage.HasValue && (dto.DeductiblePercentage.Value < 0 || dto.DeductiblePercentage.Value > 100))
            errors.Add("Deductible percentage must be between 0 and 100.");

        if (dto.Exclusions != null && dto.Exclusions.Length > 2000)
            errors.Add("Exclusions text cannot exceed 2000 characters.");

        if (dto.ExpiryDate.HasValue && dto.ExpiryDate.Value == default)
            errors.Add("Expiry date is required.");

        if (dto.Status != null)
        {
            var validStatuses = new[] { "Draft", "Active", "Expired", "Lapsed", "Cancelled" };
            if (!validStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Invalid status '{dto.Status}'. Valid values: {string.Join(", ", validStatuses)}.");
        }

        return errors;
    }
}

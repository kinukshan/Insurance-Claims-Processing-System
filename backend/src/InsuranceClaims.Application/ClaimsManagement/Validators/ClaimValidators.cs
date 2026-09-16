using InsuranceClaims.Application.ClaimsManagement.DTOs;

namespace InsuranceClaims.Application.ClaimsManagement.Validators;

/// <summary>
/// Validates CreateClaimDto input.
/// </summary>
public static class CreateClaimValidator
{
    public static List<string> Validate(CreateClaimDto dto)
    {
        var errors = new List<string>();

        if (dto.PolicyId == Guid.Empty)
            errors.Add("PolicyId is required.");

        if (string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("Description is required.");

        if (dto.ClaimedAmount <= 0)
            errors.Add("ClaimedAmount must be greater than zero.");

        if (dto.IncidentDate > DateTime.UtcNow)
            errors.Add("IncidentDate cannot be in the future.");

        if (dto.IncidentDate < DateTime.UtcNow.AddYears(-10))
            errors.Add("IncidentDate cannot be more than 10 years ago.");

        if (string.IsNullOrWhiteSpace(dto.IncidentLocation))
            errors.Add("IncidentLocation is required.");

        return errors;
    }
}

/// <summary>
/// Validates UpdateClaimDto input.
/// </summary>
public static class UpdateClaimValidator
{
    public static List<string> Validate(UpdateClaimDto dto)
    {
        var errors = new List<string>();

        if (dto.ClaimedAmount.HasValue && dto.ClaimedAmount.Value <= 0)
            errors.Add("ClaimedAmount must be greater than zero.");

        if (dto.IncidentDate.HasValue && dto.IncidentDate.Value > DateTime.UtcNow)
            errors.Add("IncidentDate cannot be in the future.");

        if (dto.Description is not null && string.IsNullOrWhiteSpace(dto.Description))
            errors.Add("Description cannot be empty.");

        if (dto.IncidentLocation is not null && string.IsNullOrWhiteSpace(dto.IncidentLocation))
            errors.Add("IncidentLocation cannot be empty.");

        return errors;
    }
}

/// <summary>
/// Validates required document checklist per claim type.
/// </summary>
public static class DocumentChecklistValidator
{
    private static readonly Dictionary<string, List<string>> RequiredDocumentsByClaimType = new()
    {
        ["Auto"] = new() { "Police Report", "Photos of Damage", "Repair Estimate", "Driver License" },
        ["Home"] = new() { "Photos of Damage", "Repair Estimate", "Property Deed" },
        ["Health"] = new() { "Medical Report", "Hospital Bills", "Prescription", "Doctor Referral" },
        ["Life"] = new() { "Death Certificate", "Beneficiary ID", "Policy Document" },
        ["Travel"] = new() { "Travel Itinerary", "Receipts", "Incident Report" },
        ["Property"] = new() { "Photos of Damage", "Repair Estimate", "Property Valuation" },
        ["Liability"] = new() { "Incident Report", "Third Party Claim", "Legal Notice" },
        ["Other"] = new() { "Supporting Document" }
    };

    /// <summary>
    /// Returns the list of required documents for a given claim type.
    /// </summary>
    public static List<string> GetRequiredDocuments(string claimType)
    {
        return RequiredDocumentsByClaimType.TryGetValue(claimType, out var docs)
            ? new List<string>(docs)
            : new List<string> { "Supporting Document" };
    }

    /// <summary>
    /// Validates submitted documents against the required checklist.
    /// Returns a list of missing document types.
    /// </summary>
    public static List<string> GetMissingDocuments(string claimType, List<string> submittedDocumentTypes)
    {
        var required = GetRequiredDocuments(claimType);
        var submitted = new HashSet<string>(submittedDocumentTypes, StringComparer.OrdinalIgnoreCase);
        return required.Where(r => !submitted.Contains(r)).ToList();
    }
}

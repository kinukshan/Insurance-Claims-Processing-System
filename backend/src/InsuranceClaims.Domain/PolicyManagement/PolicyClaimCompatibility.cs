using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Authoritative single source of truth for policy type names, deterministic IDs,
/// normalization rules, and deterministic policy-to-claim compatibility validation.
/// </summary>
public static class PolicyClaimCompatibility
{
    // Canonical Policy Type Names
    public const string MotorInsurance = "Motor Insurance";
    public const string HealthInsurance = "Health Insurance";
    public const string HomeInsurance = "Home Insurance";
    public const string LifeInsurance = "Life Insurance";

    // Stable Deterministic Domain Identifiers
    public static readonly Guid MotorInsuranceId = Guid.Parse("22222222-2222-4222-8222-222222222221");
    public static readonly Guid HealthInsuranceId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid HomeInsuranceId = Guid.Parse("22222222-2222-4222-8222-222222222223");
    public static readonly Guid LifeInsuranceId = Guid.Parse("22222222-2222-4222-8222-222222222224");

    // Canonical Fixed Deductibles
    public const decimal MotorDeductible = 10000.00m;
    public const decimal HealthDeductible = 5000.00m;
    public const decimal HomeDeductible = 15000.00m;
    public const decimal LifeDeductible = 0.00m;

    /// <summary>
    /// Authoritative source of fixed deductibles by canonical policy type name or alias.
    /// Fails closed (returns null) for unknown or unsupported policy types.
    /// </summary>
    public static decimal? GetFixedDeductible(string? rawPolicyTypeName)
    {
        var normalized = NormalizePolicyType(rawPolicyTypeName);
        return normalized switch
        {
            MotorInsurance => MotorDeductible,
            HealthInsurance => HealthDeductible,
            HomeInsurance => HomeDeductible,
            LifeInsurance => LifeDeductible,
            _ => null
        };
    }

    /// <summary>
    /// Authoritative source of fixed deductibles by deterministic policy type ID.
    /// Fails closed (returns null) for unknown policy type IDs.
    /// </summary>
    public static decimal? GetFixedDeductible(Guid policyTypeId)
    {
        if (policyTypeId == MotorInsuranceId) return MotorDeductible;
        if (policyTypeId == HealthInsuranceId) return HealthDeductible;
        if (policyTypeId == HomeInsuranceId) return HomeDeductible;
        if (policyTypeId == LifeInsuranceId) return LifeDeductible;
        return null;
    }


    /// <summary>
    /// Safely normalizes raw/input policy type names with whitespace trimming,
    /// case-insensitivity, and display-alias support (e.g., "Home / Property Insurance" -> "Home Insurance").
    /// </summary>
    public static string? NormalizePolicyType(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;
        var trimmed = rawName.Trim();

        if (trimmed.Equals(MotorInsurance, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Motor", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Auto Insurance", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Comprehensive Auto", StringComparison.OrdinalIgnoreCase))
            return MotorInsurance;

        if (trimmed.Equals(HealthInsurance, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Health", StringComparison.OrdinalIgnoreCase))
            return HealthInsurance;

        if (trimmed.Equals(HomeInsurance, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Home / Property Insurance", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Property Insurance", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Home", StringComparison.OrdinalIgnoreCase))
            return HomeInsurance;

        if (trimmed.Equals(LifeInsurance, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Life", StringComparison.OrdinalIgnoreCase))
            return LifeInsurance;

        return trimmed;
    }

    /// <summary>
    /// Returns true if the policy type name corresponds to Life Insurance.
    /// </summary>
    public static bool IsLifeInsurance(string? rawName) =>
        string.Equals(NormalizePolicyType(rawName), LifeInsurance, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the policy type ID corresponds to canonical Life Insurance.
    /// </summary>
    public static bool IsLifeInsurance(Guid policyTypeId) =>
        policyTypeId == LifeInsuranceId;

    /// <summary>
    /// Returns true if the claim type is Life.
    /// </summary>
    public static bool IsLifeClaim(ClaimType claimType) =>
        claimType == ClaimType.Life;

    /// <summary>
    /// Returns true if the claim type string represents a Life claim.
    /// </summary>
    public static bool IsLifeClaim(string? claimTypeStr) =>
        string.Equals(claimTypeStr?.Trim(), "Life", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Authoritative deterministic compatibility check between policy type and claim type.
    /// FAILS CLOSED: unknown policy types or unsupported claim types always return false.
    /// </summary>
    public static bool IsCompatible(string? policyTypeName, ClaimType claimType)
    {
        var normalizedPolicy = NormalizePolicyType(policyTypeName);
        return normalizedPolicy switch
        {
            MotorInsurance => claimType == ClaimType.Motor || claimType == ClaimType.Auto,
            HealthInsurance => claimType == ClaimType.Health,
            HomeInsurance => claimType == ClaimType.Property || claimType == ClaimType.Home,
            LifeInsurance => claimType == ClaimType.Life,
            _ => false // Fail closed for any unknown or unsupported policy type
        };
    }

    /// <summary>
    /// Authoritative deterministic compatibility check when claim type is supplied as string.
    /// FAILS CLOSED.
    /// </summary>
    public static bool IsCompatible(string? policyTypeName, string? claimTypeStr)
    {
        if (string.IsNullOrWhiteSpace(claimTypeStr)) return false;
        if (Enum.TryParse<ClaimType>(claimTypeStr.Trim(), true, out var parsedClaimType))
        {
            return IsCompatible(policyTypeName, parsedClaimType);
        }
        return false;
    }

    /// <summary>
    /// Returns the primary modern claim type for a given policy type.
    /// </summary>
    public static ClaimType GetPrimaryClaimType(string? policyTypeName)
    {
        var normalizedPolicy = NormalizePolicyType(policyTypeName);
        return normalizedPolicy switch
        {
            MotorInsurance => ClaimType.Motor,
            HealthInsurance => ClaimType.Health,
            HomeInsurance => ClaimType.Property,
            LifeInsurance => ClaimType.Life,
            _ => throw new ArgumentException($"Unknown or unsupported policy type: '{policyTypeName}'")
        };
    }

    /// <summary>
    /// Generates the standard controlled error message for policy/claim incompatibility.
    /// </summary>
    public static string GetErrorMessage(string? policyTypeName, ClaimType claimType) =>
        $"Claim type {claimType} is not compatible with {policyTypeName ?? "selected policy"}.";

    /// <summary>
    /// Generates the standard controlled error message for policy/claim incompatibility when claim type is string.
    /// </summary>
    public static string GetErrorMessage(string? policyTypeName, string? claimTypeStr) =>
        $"Claim type {claimTypeStr ?? "Unknown"} is not compatible with {policyTypeName ?? "selected policy"}.";
}

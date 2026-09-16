namespace InsuranceClaims.Domain.PolicyManagement.Enums;

/// <summary>
/// Represents the lifecycle status of an insurance policy.
/// </summary>
public enum PolicyStatus
{
    /// <summary>Policy is being drafted and not yet active.</summary>
    Draft = 0,

    /// <summary>Policy is currently active and in force.</summary>
    Active = 1,

    /// <summary>Policy has passed its expiry date.</summary>
    Expired = 2,

    /// <summary>Policy lapsed due to non-payment or non-renewal.</summary>
    Lapsed = 3,

    /// <summary>Policy was explicitly cancelled.</summary>
    Cancelled = 4
}

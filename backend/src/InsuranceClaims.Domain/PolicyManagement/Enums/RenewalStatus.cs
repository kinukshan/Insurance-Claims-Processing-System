namespace InsuranceClaims.Domain.PolicyManagement.Enums;

/// <summary>
/// Represents the renewal status of an insurance policy.
/// </summary>
public enum RenewalStatus
{
    /// <summary>Renewal is not yet due.</summary>
    NotDue = 0,

    /// <summary>Renewal is pending decision.</summary>
    Pending = 1,

    /// <summary>Policy has been renewed.</summary>
    Renewed = 2,

    /// <summary>Renewal was declined by the policyholder.</summary>
    Declined = 3
}

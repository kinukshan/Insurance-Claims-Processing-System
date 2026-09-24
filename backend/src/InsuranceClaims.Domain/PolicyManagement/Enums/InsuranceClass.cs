namespace InsuranceClaims.Domain.PolicyManagement.Enums;

/// <summary>
/// High-level insurance classification reflecting regulatory and reporting standards.
/// NOTE: This is strictly classification/reporting metadata. Specific product rules
/// (such as deductibles or payout terms) are driven by the specific PolicyType.
/// </summary>
public enum InsuranceClass
{
    General = 0,
    LongTerm = 1
}

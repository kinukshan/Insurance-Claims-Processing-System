namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Categorizes the type of insurance claim being submitted.
/// </summary>
public enum ClaimType
{
    Auto = 0,
    Home = 1,
    Health = 2,
    Life = 3,
    Travel = 4,
    Property = 5,
    Liability = 6,
    Other = 7,
    Motor = 8
}

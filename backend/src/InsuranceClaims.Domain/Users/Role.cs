namespace InsuranceClaims.Domain.Users;

/// <summary>
/// User roles in the insurance claims system.
/// </summary>
public enum Role
{
    Policyholder = 0,
    ClaimsAdjuster = 1,
    Underwriter = 2,
    Admin = 3
}

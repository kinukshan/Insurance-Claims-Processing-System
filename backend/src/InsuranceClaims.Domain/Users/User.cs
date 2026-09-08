using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.Users;

/// <summary>
/// Represents a system user (Policyholder, Claims Adjuster, Underwriter/Admin).
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool IsActive { get; set; } = true;
}

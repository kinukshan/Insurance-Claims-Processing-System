namespace InsuranceClaims.Infrastructure.Authentication;

/// <summary>
/// Password hashing and verification service using BCrypt.
/// </summary>
public class PasswordService
{
    /// <summary>
    /// Hash a plaintext password using BCrypt.
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// Verify a plaintext password against a BCrypt hash.
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

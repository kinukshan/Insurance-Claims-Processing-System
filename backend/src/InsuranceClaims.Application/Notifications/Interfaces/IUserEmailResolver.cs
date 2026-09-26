namespace InsuranceClaims.Application.Notifications.Interfaces;

/// <summary>
/// Resolves user email addresses from the backend database.
/// Recipients are ALWAYS resolved server-side — never from frontend input.
/// </summary>
public interface IUserEmailResolver
{
    /// <summary>
    /// Look up a user's email address by their ID.
    /// Returns null if the user is not found.
    /// </summary>
    Task<string?> GetEmailAsync(Guid userId);
}

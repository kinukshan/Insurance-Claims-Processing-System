namespace InsuranceClaims.Domain.PolicyManagement.Exceptions;

/// <summary>
/// Dedicated exception thrown when a claim type is incompatible with a policy's type.
/// Specifically maps to HTTP 400 Bad Request at the API boundary without conflating
/// with other business or workflow exceptions (such as 409 Conflict).
/// </summary>
public class PolicyClaimCompatibilityException : Exception
{
    public PolicyClaimCompatibilityException(string message) : base(message)
    {
    }

    public PolicyClaimCompatibilityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

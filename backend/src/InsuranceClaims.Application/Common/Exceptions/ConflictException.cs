namespace InsuranceClaims.Application.Common.Exceptions;

/// <summary>
/// Thrown when a resource operation results in a conflict (e.g. deleting a policy referenced by claims).
/// Mapped to HTTP 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

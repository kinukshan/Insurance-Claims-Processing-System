using System;

namespace InsuranceClaims.Domain.Common;

/// <summary>
/// Base entity with shared audit fields.
/// All domain entities inherit from this.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

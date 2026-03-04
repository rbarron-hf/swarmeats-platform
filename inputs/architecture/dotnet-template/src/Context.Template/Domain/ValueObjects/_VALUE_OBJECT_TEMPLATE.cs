namespace {ContextName}.Domain.ValueObjects;

/// <summary>
/// Value object representing {description}.
///
/// Value objects are immutable and compared by structural equality (record).
/// Validation is enforced in the constructor — invalid instances cannot exist.
///
/// Story: {STORY_ID}
/// </summary>
public sealed record {ValueObjectName}
{
    /// <summary>Example property — replace with actual fields.</summary>
    public required {PropertyType} {PropertyName} { get; init; }

    /// <summary>
    /// Creates a validated {ValueObjectName}.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when validation rules are violated.
    /// </exception>
    public {ValueObjectName}
    {
        // ── Validation ──
        // TODO: Add validation rules
        // if (string.IsNullOrWhiteSpace(PropertyName))
        //     throw new ArgumentException(
        //         $"{nameof(PropertyName)} cannot be empty.",
        //         nameof(PropertyName));
    }
}

// ══════════════════════════════════════════════════════════════
// Enum-style value object alternative (for status / type fields)
// ══════════════════════════════════════════════════════════════
//
// public enum {StatusName}
// {
//     /// <summary>Initial state.</summary>
//     Pending = 0,
//
//     /// <summary>Actively being processed.</summary>
//     Active = 1,
//
//     /// <summary>Successfully completed.</summary>
//     Completed = 2,
//
//     /// <summary>Terminated before completion.</summary>
//     Cancelled = 3
// }

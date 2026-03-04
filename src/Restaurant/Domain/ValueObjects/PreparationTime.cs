namespace Restaurant.Domain.ValueObjects;

/// <summary>
/// Value object representing the estimated preparation time in minutes for a menu item.
/// Must be at least 1 minute. Enforces RST-R06.
/// </summary>
public sealed record PreparationTime
{
    /// <summary>
    /// Estimated preparation time in minutes. Must be at least 1.
    /// </summary>
    public int Minutes { get; init; }

    public PreparationTime(int minutes)
    {
        if (minutes < 1)
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Preparation time must be at least 1 minute.");

        Minutes = minutes;
    }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private PreparationTime() { }
}

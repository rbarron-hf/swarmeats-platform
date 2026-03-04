namespace Restaurant.Domain.ValueObjects;

/// <summary>
/// Value object representing a restaurant's operating hours window in UTC.
/// Used for RST-R01 validation: orders placed outside operating hours are auto-rejected.
/// </summary>
public sealed record OperatingHours
{
    /// <summary>
    /// The opening time in UTC (e.g., "09:00").
    /// </summary>
    public string OpeningTime { get; init; } = string.Empty;

    /// <summary>
    /// The closing time in UTC (e.g., "22:00").
    /// </summary>
    public string ClosingTime { get; init; } = string.Empty;

    public OperatingHours(string openingTime, string closingTime)
    {
        if (string.IsNullOrWhiteSpace(openingTime))
            throw new ArgumentException("Opening time must not be empty.", nameof(openingTime));
        if (string.IsNullOrWhiteSpace(closingTime))
            throw new ArgumentException("Closing time must not be empty.", nameof(closingTime));

        OpeningTime = openingTime;
        ClosingTime = closingTime;
    }

    /// <summary>
    /// Determines whether the specified UTC time falls within operating hours.
    /// </summary>
    /// <param name="utcNow">The current UTC time to check.</param>
    /// <returns>True if within operating hours; false otherwise.</returns>
    public bool IsWithinOperatingHours(DateTimeOffset utcNow)
    {
        if (!TimeOnly.TryParse(OpeningTime, out var open) ||
            !TimeOnly.TryParse(ClosingTime, out var close))
        {
            return false;
        }

        var currentTime = TimeOnly.FromDateTime(utcNow.UtcDateTime);

        // Handle overnight hours (e.g., 22:00 to 04:00)
        if (close < open)
        {
            return currentTime >= open || currentTime <= close;
        }

        return currentTime >= open && currentTime <= close;
    }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private OperatingHours() { }
}

namespace Restaurant.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount with currency.
/// Price must be greater than zero. Currency is always "GBP" for the initial release.
/// Enforces RST-R06.
/// </summary>
public sealed record Price
{
    /// <summary>
    /// The monetary amount. Must be greater than zero.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// The currency code. Always "GBP" for the initial release.
    /// </summary>
    public string Currency { get; init; } = "GBP";

    public Price(decimal amount, string currency = "GBP")
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Price amount must be greater than zero.");

        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private Price() { }
}

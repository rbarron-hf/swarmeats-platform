namespace Restaurant.Domain.Exceptions;

/// <summary>
/// Thrown when one or more line items reference menu items that are unavailable or do not exist.
/// Used in RST-003 for auto-rejection per business rule RST-R02.
/// </summary>
public sealed class MenuItemUnavailableException : Exception
{
    public List<Guid> UnavailableItemIds { get; }
    public string ErrorCode => "ITEM_UNAVAILABLE";

    public MenuItemUnavailableException(List<Guid> unavailableItemIds)
        : base($"The following menu items are unavailable: {string.Join(", ", unavailableItemIds)}")
    {
        UnavailableItemIds = unavailableItemIds;
    }

    public MenuItemUnavailableException(List<Guid> unavailableItemIds, Exception innerException)
        : base($"The following menu items are unavailable: {string.Join(", ", unavailableItemIds)}", innerException)
    {
        UnavailableItemIds = unavailableItemIds;
    }
}

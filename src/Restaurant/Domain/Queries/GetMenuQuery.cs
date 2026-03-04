using MediatR;

namespace Restaurant.Domain.Queries;

/// <summary>
/// Query to retrieve the full menu for a restaurant including all items, availability, and operating hours.
/// Dispatched from GetMenuFunction to the GetMenuQueryHandler via MediatR.
/// This is a read-only operation — no state changes or domain events are produced.
/// RST-001.
/// </summary>
public sealed record GetMenuQuery : IRequest<GetMenuResponse>
{
    /// <summary>
    /// The restaurant identifier to look up the menu for.
    /// </summary>
    public required Guid RestaurantId { get; init; }
}

/// <summary>
/// Response DTO containing the full restaurant menu.
/// </summary>
public sealed record GetMenuResponse
{
    public required Guid RestaurantId { get; init; }
    public required GetMenuOperatingHoursResponse OperatingHours { get; init; }
    public required List<GetMenuItemResponse> Items { get; init; }
}

/// <summary>
/// Operating hours within a GetMenuResponse.
/// </summary>
public sealed record GetMenuOperatingHoursResponse
{
    public required string OpeningTime { get; init; }
    public required string ClosingTime { get; init; }
}

/// <summary>
/// Menu item details within a GetMenuResponse.
/// </summary>
public sealed record GetMenuItemResponse
{
    public required Guid MenuItemId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required GetMenuPriceResponse Price { get; init; }
    public required string Category { get; init; }
    public required bool IsAvailable { get; init; }
    public required int PreparationTimeMinutes { get; init; }
}

/// <summary>
/// Price details within a GetMenuItemResponse.
/// </summary>
public sealed record GetMenuPriceResponse
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
}

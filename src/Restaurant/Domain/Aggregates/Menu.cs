using Restaurant.Domain.ValueObjects;

namespace Restaurant.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a restaurant's active menu.
/// One active menu per restaurant. Contains menu items, operating hours, and availability.
/// Partition key in Cosmos DB menus container: restaurantId.
/// </summary>
public class Menu : AggregateRoot<Guid>
{
    /// <summary>
    /// The restaurant identifier. Also serves as the Cosmos DB partition key.
    /// </summary>
    public Guid RestaurantId { get; private set; }

    /// <summary>
    /// The name of the restaurant.
    /// </summary>
    public string RestaurantName { get; private set; } = string.Empty;

    /// <summary>
    /// The restaurant's operating hours in UTC.
    /// </summary>
    public OperatingHours? OperatingHours { get; private set; }

    /// <summary>
    /// The restaurant's physical address.
    /// </summary>
    public RestaurantMenuAddress? Address { get; private set; }

    /// <summary>
    /// The list of items on the menu.
    /// </summary>
    public List<MenuItem> MenuItems { get; private set; } = new();

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private Menu() { }

    /// <summary>
    /// Creates a new Menu aggregate.
    /// </summary>
    public Menu(
        Guid id,
        Guid restaurantId,
        string restaurantName,
        OperatingHours operatingHours,
        RestaurantMenuAddress? address = null)
    {
        Id = id;
        RestaurantId = restaurantId;
        RestaurantName = restaurantName;
        OperatingHours = operatingHours;
        Address = address;
    }

    /// <summary>
    /// Checks whether the restaurant is currently within operating hours.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>True if within operating hours; false otherwise.</returns>
    public bool IsOpen(DateTimeOffset utcNow)
    {
        return OperatingHours?.IsWithinOperatingHours(utcNow) ?? false;
    }

    /// <summary>
    /// Validates that all requested menu item IDs exist in the menu and are available.
    /// Returns the list of unavailable item IDs.
    /// </summary>
    /// <param name="requestedMenuItemIds">The menu item IDs to validate.</param>
    /// <returns>List of menu item IDs that are unavailable or do not exist.</returns>
    public List<Guid> GetUnavailableItemIds(IEnumerable<Guid> requestedMenuItemIds)
    {
        var unavailable = new List<Guid>();
        foreach (var requestedId in requestedMenuItemIds)
        {
            var menuItem = MenuItems.FirstOrDefault(mi => mi.MenuItemId == requestedId);
            if (menuItem is null || !menuItem.IsAvailable)
            {
                unavailable.Add(requestedId);
            }
        }
        return unavailable;
    }
}

/// <summary>
/// Entity representing a single item on a restaurant's menu.
/// </summary>
public class MenuItem
{
    /// <summary>
    /// Unique identifier for the menu item.
    /// </summary>
    public Guid MenuItemId { get; set; }

    /// <summary>
    /// Name of the menu item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the menu item.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Price of the menu item.
    /// </summary>
    public Price? Price { get; set; }

    /// <summary>
    /// Category of the menu item (e.g., "Starters", "Mains", "Desserts").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Estimated preparation time in minutes.
    /// </summary>
    public PreparationTime? PreparationTime { get; set; }

    /// <summary>
    /// Whether the menu item is currently available for ordering.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    public MenuItem() { }

    /// <summary>
    /// Creates a new MenuItem entity.
    /// </summary>
    public MenuItem(Guid menuItemId, string name, string description, Price price, string category, PreparationTime preparationTime, bool isAvailable)
    {
        MenuItemId = menuItemId;
        Name = name;
        Description = description;
        Price = price;
        Category = category;
        PreparationTime = preparationTime;
        IsAvailable = isAvailable;
    }
}

/// <summary>
/// Value object for the restaurant's physical address stored on the Menu.
/// </summary>
public record RestaurantMenuAddress(
    string Street,
    string City,
    string Postcode,
    decimal Latitude,
    decimal Longitude);

using Restaurant.Domain.Aggregates;

namespace Restaurant.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the Menu aggregate.
/// Provides read access to restaurant menus stored in the Cosmos DB menus container.
/// </summary>
public interface IMenuRepository
{
    /// <summary>
    /// Loads a Menu aggregate by the restaurant identifier.
    /// Returns null if no menu exists for the given restaurant.
    /// </summary>
    /// <param name="restaurantId">The restaurant identifier (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Menu aggregate, or null if not found.</returns>
    Task<Menu?> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}

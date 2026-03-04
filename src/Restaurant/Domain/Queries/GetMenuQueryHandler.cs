using MediatR;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Exceptions;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Queries;

/// <summary>
/// Handles the GetMenuQuery by loading the Menu aggregate from the repository
/// and mapping it to a GetMenuResponse DTO. This is a pure read operation —
/// no business logic, no state changes, no domain events.
/// RST-001.
/// </summary>
public sealed class GetMenuQueryHandler : IRequestHandler<GetMenuQuery, GetMenuResponse>
{
    private readonly IMenuRepository _menuRepository;

    public GetMenuQueryHandler(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
    }

    public async Task<GetMenuResponse> Handle(GetMenuQuery request, CancellationToken cancellationToken)
    {
        var menu = await _menuRepository.GetByRestaurantIdAsync(request.RestaurantId, cancellationToken);

        if (menu is null)
        {
            throw new MenuNotFoundException(request.RestaurantId);
        }

        return MapToResponse(menu);
    }

    private static GetMenuResponse MapToResponse(Menu menu)
    {
        return new GetMenuResponse
        {
            RestaurantId = menu.RestaurantId,
            OperatingHours = new GetMenuOperatingHoursResponse
            {
                OpeningTime = menu.OperatingHours?.OpeningTime ?? string.Empty,
                ClosingTime = menu.OperatingHours?.ClosingTime ?? string.Empty
            },
            Items = menu.MenuItems.Select(mi => new GetMenuItemResponse
            {
                MenuItemId = mi.MenuItemId,
                Name = mi.Name,
                Description = mi.Description,
                Price = new GetMenuPriceResponse
                {
                    Amount = mi.Price?.Amount ?? 0,
                    Currency = mi.Price?.Currency ?? "GBP"
                },
                Category = mi.Category,
                IsAvailable = mi.IsAvailable,
                PreparationTimeMinutes = mi.PreparationTime?.Minutes ?? 0
            }).ToList()
        };
    }
}

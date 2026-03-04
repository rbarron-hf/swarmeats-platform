namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Static helper class providing geographic distance calculations using the Haversine formula.
/// Used by the Delivery aggregate to enforce DLV-R02 (driver must be within 5km of restaurant)
/// and to calculate estimated arrival times.
/// </summary>
public static class GeoCalculations
{
    /// <summary>
    /// Earth's mean radius in kilometres.
    /// </summary>
    private const double EarthRadiusKm = 6371;

    /// <summary>
    /// Average driver speed in km/h used for estimated arrival calculation.
    /// Assumes urban driving conditions.
    /// </summary>
    public const double AverageDriverSpeedKmh = 30;

    /// <summary>
    /// Maximum allowed distance in kilometres between driver and restaurant for assignment (DLV-R02).
    /// </summary>
    public const double MaxAssignmentDistanceKm = 5.0;

    /// <summary>
    /// SLA threshold in minutes from OrderReadyForPickup to Delivered (DLV-R03).
    /// </summary>
    public const int SlaThresholdMinutes = 45;

    /// <summary>
    /// Calculates the straight-line distance in kilometres between two geographic points
    /// using the Haversine formula. This is the great-circle distance on a sphere.
    /// </summary>
    /// <param name="lat1">Latitude of the first point in degrees.</param>
    /// <param name="lon1">Longitude of the first point in degrees.</param>
    /// <param name="lat2">Latitude of the second point in degrees.</param>
    /// <param name="lon2">Longitude of the second point in degrees.</param>
    /// <returns>Distance between the two points in kilometres.</returns>
    public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Estimates the travel time in minutes between two points based on straight-line
    /// distance and an assumed average driver speed of 30 km/h.
    /// </summary>
    /// <param name="lat1">Latitude of the origin in degrees.</param>
    /// <param name="lon1">Longitude of the origin in degrees.</param>
    /// <param name="lat2">Latitude of the destination in degrees.</param>
    /// <param name="lon2">Longitude of the destination in degrees.</param>
    /// <returns>Estimated travel time in whole minutes (rounded up).</returns>
    public static int EstimateTravelMinutes(double lat1, double lon1, double lat2, double lon2)
    {
        var distanceKm = CalculateDistanceKm(lat1, lon1, lat2, lon2);
        var travelHours = distanceKm / AverageDriverSpeedKmh;
        return (int)Math.Ceiling(travelHours * 60);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

using FluentAssertions;
using Delivery.Domain.ValueObjects;
using Xunit;

namespace Delivery.Tests.Unit;

/// <summary>
/// Unit tests for the GeoCalculations static helper class.
/// Tests the Haversine distance formula with known geographic distances.
/// </summary>
public class GeoCalculationsTests
{
    /// <summary>
    /// London (51.5074, -0.1278) to Paris (48.8566, 2.3522) is approximately 343km.
    /// </summary>
    [Fact]
    public void CalculateDistanceKm_LondonToParis_ReturnsApproximately343Km()
    {
        // Arrange
        const double londonLat = 51.5074;
        const double londonLon = -0.1278;
        const double parisLat = 48.8566;
        const double parisLon = 2.3522;

        // Act
        var distance = GeoCalculations.CalculateDistanceKm(londonLat, londonLon, parisLat, parisLon);

        // Assert
        distance.Should().BeApproximately(343, 5, "London to Paris is approximately 343km");
    }

    /// <summary>
    /// Same point should return zero distance.
    /// </summary>
    [Fact]
    public void CalculateDistanceKm_SamePoint_ReturnsZero()
    {
        // Arrange
        const double lat = 51.5074;
        const double lon = -0.1278;

        // Act
        var distance = GeoCalculations.CalculateDistanceKm(lat, lon, lat, lon);

        // Assert
        distance.Should().Be(0);
    }

    /// <summary>
    /// Two points within 5km should be within the assignment threshold.
    /// Central London (51.5074, -0.1278) to near Tower Bridge (51.5055, -0.0754) is about 3.6km.
    /// </summary>
    [Fact]
    public void CalculateDistanceKm_NearbyPoints_ReturnsDistanceWithin5Km()
    {
        // Arrange
        const double lat1 = 51.5074;
        const double lon1 = -0.1278;
        const double lat2 = 51.5055;
        const double lon2 = -0.0754;

        // Act
        var distance = GeoCalculations.CalculateDistanceKm(lat1, lon1, lat2, lon2);

        // Assert
        distance.Should().BeLessThan(GeoCalculations.MaxAssignmentDistanceKm,
            "nearby points in central London should be within the 5km assignment threshold");
    }

    /// <summary>
    /// New York (40.7128, -74.0060) to Los Angeles (34.0522, -118.2437) is approximately 3944km.
    /// </summary>
    [Fact]
    public void CalculateDistanceKm_NewYorkToLosAngeles_ReturnsApproximately3944Km()
    {
        // Arrange
        const double nyLat = 40.7128;
        const double nyLon = -74.0060;
        const double laLat = 34.0522;
        const double laLon = -118.2437;

        // Act
        var distance = GeoCalculations.CalculateDistanceKm(nyLat, nyLon, laLat, laLon);

        // Assert
        distance.Should().BeApproximately(3944, 50, "New York to Los Angeles is approximately 3944km");
    }

    /// <summary>
    /// Estimated travel minutes for a 30km distance at 30km/h should be approximately 60 minutes.
    /// </summary>
    [Fact]
    public void EstimateTravelMinutes_KnownDistance_ReturnsReasonableEstimate()
    {
        // Arrange - Using two points approximately 5km apart
        const double lat1 = 51.5074;
        const double lon1 = -0.1278;
        const double lat2 = 51.5074;
        const double lon2 = -0.0578; // roughly 4.8km east at this latitude

        // Act
        var minutes = GeoCalculations.EstimateTravelMinutes(lat1, lon1, lat2, lon2);

        // Assert
        minutes.Should().BeGreaterThan(0, "travel time should be positive for non-zero distance");
        minutes.Should().BeLessThan(60, "a short distance should not take an hour");
    }

    /// <summary>
    /// Travel time for the same point should be zero.
    /// </summary>
    [Fact]
    public void EstimateTravelMinutes_SamePoint_ReturnsZero()
    {
        // Arrange
        const double lat = 51.5074;
        const double lon = -0.1278;

        // Act
        var minutes = GeoCalculations.EstimateTravelMinutes(lat, lon, lat, lon);

        // Assert
        minutes.Should().Be(0);
    }

    /// <summary>
    /// The MaxAssignmentDistanceKm constant should be 5km as per DLV-R02.
    /// </summary>
    [Fact]
    public void MaxAssignmentDistanceKm_ShouldBe5()
    {
        GeoCalculations.MaxAssignmentDistanceKm.Should().Be(5.0);
    }

    /// <summary>
    /// The SlaThresholdMinutes constant should be 45 as per DLV-R03.
    /// </summary>
    [Fact]
    public void SlaThresholdMinutes_ShouldBe45()
    {
        GeoCalculations.SlaThresholdMinutes.Should().Be(45);
    }
}

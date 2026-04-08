using friends.api.Models;

namespace friends.api.Services;

public class LocationService : ILocationService
{
    private const double EarthRadiusMiles = 3958.8;

    public double? GetDistanceMiles(User origin, User candidate)
    {
        if (origin.Latitude is null ||
            origin.Longitude is null ||
            candidate.Latitude is null ||
            candidate.Longitude is null)
        {
            return null;
        }

        var originLatitude = DegreesToRadians(origin.Latitude.Value);
        var candidateLatitude = DegreesToRadians(candidate.Latitude.Value);
        var latitudeDelta = DegreesToRadians(candidate.Latitude.Value - origin.Latitude.Value);
        var longitudeDelta = DegreesToRadians(candidate.Longitude.Value - origin.Longitude.Value);

        var angle = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
                    Math.Cos(originLatitude) * Math.Cos(candidateLatitude) *
                    Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);

        var centralAngle = 2 * Math.Atan2(Math.Sqrt(angle), Math.Sqrt(1 - angle));

        return EarthRadiusMiles * centralAngle;
    }

    public bool IsWithinRange(User origin, User candidate, double radiusMiles)
    {
        var distance = GetDistanceMiles(origin, candidate);

        return distance is not null && distance <= radiusMiles;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}

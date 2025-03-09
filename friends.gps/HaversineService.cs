// HAVERSINE REFERENCE SCRIPT: https://gist.github.com/jammin77/033a332542aa24889452
/// <summary>
/// The distance type to return the results in.
/// </summary>
public static class HaversineService
{
    public enum DistanceType
    {
        Miles,
        Kilometers
    };

    /// <summary>
    /// Returns the distance in miles or kilometers of any two
    /// latitude / longitude points.
    /// </summary>
    /// <param name=”pos1″></param>
    /// <param name=”pos2″></param>
    /// <param name=”type”></param>
    /// <returns></returns>
    public static double GetDistance(this LocationTimeStamp pos1, LocationTimeStamp pos2, DistanceType type = DistanceType.Kilometers)
    {
        // Get Distance Ref Type
        double R = (type == DistanceType.Miles) ? 3960 : 6371;

        // Get Longitude Difference
        double dLat = toRadian(pos2.Longitude - pos1.Longitude);
        // Get Latitude Difference
        double dLon = toRadian(pos2.Latitude - pos1.Latitude);

        // accumulative difference in latitude and longitude
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(toRadian(pos1.Longitude)) * Math.Cos(toRadian(pos2.Latitude)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        // Output distance
        double c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));

        // Reference * Output = (Double) Distance between geolocations
        double d = R * c;

        return d;
    }

    /// <summary>
    /// Convert to Radians.
    /// </summary>
    /// <param name=”val”></param>
    /// <returns></returns>
    private static double toRadian(double val)
    {
        return (Math.PI / 180) * val;
    }
}
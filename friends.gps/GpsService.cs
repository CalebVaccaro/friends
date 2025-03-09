using System.Collections.Concurrent;

namespace friends.gps;

public class GpsService
{
    private ConcurrentDictionary<string, LocationTimeStamp> _locations = new();
    
    public void AddLocation(string userId, LocationTimeStamp locationTimeStamp)
    {
        _locations.AddOrUpdate(userId, locationTimeStamp, (key, oldValue) => locationTimeStamp);
    }
    
    public LocationTimeStamp GetLocation(string userId)
    {
        var location = _locations.Where(e => e.Key == userId)
            .Select(e => e.Value)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();
        return location;
    }

    public double CalculateDistance(LocationTimeStamp location1, LocationTimeStamp location2)
    {
        return location1.GetDistance(location2);
    }
}
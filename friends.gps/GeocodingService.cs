using Geocoding;
using Geocoding.Microsoft;
using Microsoft.Extensions.Configuration;

namespace friends.gps;

public static class GeocodingService
{
    public static async Task<Location> GetCoordinates(string _address)
    {
        IConfiguration configuration = ConfigurationLoader.LoadConfiguration();
        var apiKey = configuration.GetConnectionString("BingGeocodingApiKey");
        
        IGeocoder geocoder = new BingMapsGeocoder(apiKey);
        IEnumerable<Address> addresses = await geocoder.GeocodeAsync(_address);
        var address = addresses.FirstOrDefault();
        var coordinates = address?.Coordinates;
        return coordinates;
    }
}
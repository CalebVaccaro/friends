// See https://aka.ms/new-console-template for more information

using friends.gps;

Console.WriteLine("Hello, World!");

var gpsService = new GpsService();

// Get coordinates for an address
var address = "783 Knickerbocker Ave, Brooklyn, NY 11207";
var coordinates = await GeocodingService.GetCoordinates(address);

// Create a user location
var user = new LocationTimeStamp(coordinates.Latitude, coordinates.Longitude, DateTime.UtcNow);
Console.WriteLine($"User location: {user.Latitude}, {user.Longitude}");
var friend = new LocationTimeStamp(34.7749, -122.4194, DateTime.UtcNow);

// Calculate distance between user and friend
var distance = gpsService.CalculateDistance(user, friend);
Console.WriteLine($"Distance between user and friend: {distance} km");

public record LocationTimeStamp(double Latitude, double Longitude, DateTime Timestamp);

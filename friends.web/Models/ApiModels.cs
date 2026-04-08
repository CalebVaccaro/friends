using System.Text.Json.Serialization;

namespace friends.web.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string LocationLabel { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = [];
    public List<string> Interests { get; set; } = [];
}

public class UserProfileSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string LocationLabel { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = [];
    public List<string> Interests { get; set; } = [];
}

public class UserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string LocationLabel { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = [];
    public List<string> Interests { get; set; } = [];
}

public class UserDiscoveryResult
{
    public UserProfileSummary User { get; set; } = new();
    public double? DistanceMiles { get; set; }
    public List<string> SharedInterests { get; set; } = [];
}

public class SwipeRequest
{
    public Guid TargetUserId { get; set; }
    public SwipeAction Action { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SwipeAction
{
    Pass,
    Like
}

public class SwipeResult
{
    public bool IsMatch { get; set; }
    public UserProfileSummary? MatchedUser { get; set; }
}

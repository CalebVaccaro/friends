namespace friends.api.Models;

public class UserDiscoveryResult
{
    public required UserProfileSummary User { get; set; }
    public double? DistanceMiles { get; set; }
    public List<string> SharedInterests { get; set; } = [];
}

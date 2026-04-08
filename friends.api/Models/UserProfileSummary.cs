namespace friends.api.Models;

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

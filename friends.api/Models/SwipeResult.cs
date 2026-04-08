namespace friends.api.Models;

public class SwipeResult
{
    public required UserSwipe Swipe { get; set; }
    public bool IsMatch { get; set; }
    public FriendConnection? Connection { get; set; }
    public UserProfileSummary? MatchedUser { get; set; }
}

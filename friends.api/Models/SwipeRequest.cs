namespace friends.api.Models;

public class SwipeRequest
{
    public Guid TargetUserId { get; set; }
    public SwipeAction Action { get; set; }
}

namespace friends.api.Models;

public class UserSwipe
{
    public Guid Id { get; set; }
    public Guid SwiperUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public SwipeAction Action { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

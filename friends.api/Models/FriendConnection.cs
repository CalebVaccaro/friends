namespace friends.api.Models;

public class FriendConnection
{
    public Guid Id { get; set; }
    public Guid UserAId { get; set; }
    public Guid UserBId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

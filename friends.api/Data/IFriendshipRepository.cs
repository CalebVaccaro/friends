using friends.api.Models;

namespace friends.api.Data;

public interface IFriendshipRepository
{
    Task<IReadOnlyList<UserSwipe>> GetSwipesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SwipeResult> SwipeAsync(Guid swiperUserId, SwipeRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteConnectionAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}

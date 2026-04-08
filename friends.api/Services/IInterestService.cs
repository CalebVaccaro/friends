using friends.api.Models;

namespace friends.api.Services;

public interface IInterestService
{
    List<string> GetSharedInterests(User origin, User candidate, IReadOnlyCollection<string>? requestedInterests = null);
    bool HasAnySharedInterest(User origin, User candidate, IReadOnlyCollection<string>? requestedInterests = null);
}

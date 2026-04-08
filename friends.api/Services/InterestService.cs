using friends.api.Models;

namespace friends.api.Services;

public class InterestService : IInterestService
{
    public List<string> GetSharedInterests(User origin, User candidate, IReadOnlyCollection<string>? requestedInterests = null)
    {
        var originInterests = Normalize(requestedInterests is { Count: > 0 } ? requestedInterests : origin.Interests);
        var candidateInterests = Normalize(candidate.Interests);

        return originInterests
            .Where(candidateInterests.Contains)
            .OrderBy(interest => interest)
            .ToList();
    }

    public bool HasAnySharedInterest(User origin, User candidate, IReadOnlyCollection<string>? requestedInterests = null)
    {
        return GetSharedInterests(origin, candidate, requestedInterests).Count > 0;
    }

    private static HashSet<string> Normalize(IEnumerable<string> interests)
    {
        return interests
            .Where(interest => !string.IsNullOrWhiteSpace(interest))
            .Select(interest => interest.Trim().ToLowerInvariant())
            .ToHashSet();
    }
}

using friends.api.Models;

namespace friends.api.Services;

public interface ILocationService
{
    double? GetDistanceMiles(User origin, User candidate);
    bool IsWithinRange(User origin, User candidate, double radiusMiles);
}

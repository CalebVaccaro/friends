using System.Net.Http.Json;
using friends.web.Models;

namespace friends.web.Services;

public class FriendsApiClient
{
    private readonly HttpClient _httpClient;

    public FriendsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<User>>("/api/users", cancellationToken) ?? [];
    }

    public async Task<User> CreateUserAsync(UserRequest user, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/users", user, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<User>(cancellationToken) ?? new User();
    }

    public async Task<User?> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = CreateCurrentUserRequest(HttpMethod.Get, "/api/me", userId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<User>(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> SeedUsersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync("/api/test/users", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<User>>(cancellationToken) ?? [];
    }

    public async Task ResetTestDataAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("/api/test/data", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<UserDiscoveryResult>> DiscoverAsync(Guid userId, int? radiusMiles, CancellationToken cancellationToken = default)
    {
        var url = radiusMiles is null
            ? "/api/me/discover"
            : $"/api/me/discover?radiusMiles={radiusMiles}";

        using var request = CreateCurrentUserRequest(HttpMethod.Get, url, userId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<UserDiscoveryResult>>(cancellationToken) ?? [];
    }

    public async Task<SwipeResult> SwipeAsync(Guid userId, Guid targetUserId, SwipeAction action, CancellationToken cancellationToken = default)
    {
        using var request = CreateCurrentUserRequest(HttpMethod.Post, "/api/me/swipes", userId);
        request.Content = JsonContent.Create(new SwipeRequest
        {
            TargetUserId = targetUserId,
            Action = action
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SwipeResult>(cancellationToken) ?? new SwipeResult();
    }

    public async Task<IReadOnlyList<UserProfileSummary>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var request = CreateCurrentUserRequest(HttpMethod.Get, "/api/me/connections", userId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<UserProfileSummary>>(cancellationToken) ?? [];
    }

    private static HttpRequestMessage CreateCurrentUserRequest(HttpMethod method, string url, Guid userId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-User-Id", userId.ToString());

        return request;
    }
}

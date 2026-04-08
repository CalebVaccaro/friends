using friends.api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using FriendUser = friends.api.Models.User;

namespace friends.api.Data;

public class CosmosUserRepository : IUserRepository
{
    private readonly Container _container;

    public CosmosUserRepository(IOptions<FriendsDbOptions> options)
    {
        var dbOptions = options.Value;
        var client = new CosmosClient(dbOptions.CosmosEndpoint, dbOptions.CosmosKey, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        var database = client.CreateDatabaseIfNotExistsAsync(dbOptions.CosmosDatabaseName).GetAwaiter().GetResult().Database;
        _container = database.CreateContainerIfNotExistsAsync(dbOptions.CosmosContainerName, "/id").GetAwaiter().GetResult().Container;
    }

    public async Task<IReadOnlyList<FriendUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = new List<FriendUser>();
        using var iterator = _container.GetItemQueryIterator<FriendUser>("SELECT * FROM c ORDER BY c.createdAt DESC");

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            users.AddRange(response);
        }

        return users;
    }

    public async Task<FriendUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<FriendUser>(id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<FriendUser> CreateAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new FriendUser
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Age = request.Age,
            Bio = request.Bio,
            LocationLabel = request.LocationLabel,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PhotoUrl = request.PhotoUrl,
            PhotoUrls = NormalizePhotoUrls(request),
            Interests = request.Interests,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _container.CreateItemAsync(user, new PartitionKey(user.Id.ToString()), cancellationToken: cancellationToken);

        return user;
    }

    public async Task<FriendUser?> UpdateAsync(Guid id, UserRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(id, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        existing.Name = request.Name;
        existing.Email = request.Email;
        existing.Age = request.Age;
        existing.Bio = request.Bio;
        existing.LocationLabel = request.LocationLabel;
        existing.Latitude = request.Latitude;
        existing.Longitude = request.Longitude;
        existing.PhotoUrl = request.PhotoUrl;
        existing.PhotoUrls = NormalizePhotoUrls(request);
        existing.Interests = request.Interests;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _container.UpsertItemAsync(existing, new PartitionKey(existing.Id.ToString()), cancellationToken: cancellationToken);

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<FriendUser>(id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        using var iterator = _container.GetItemQueryIterator<FriendUser>("SELECT * FROM c");

        while (iterator.HasMoreResults)
        {
            foreach (var user in await iterator.ReadNextAsync(cancellationToken))
            {
                await _container.DeleteItemAsync<FriendUser>(user.Id.ToString(), new PartitionKey(user.Id.ToString()), cancellationToken: cancellationToken);
            }
        }
    }

    private static List<string> NormalizePhotoUrls(UserRequest request)
    {
        var photoUrls = request.PhotoUrls
            .Where(photoUrl => !string.IsNullOrWhiteSpace(photoUrl))
            .Select(photoUrl => photoUrl.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.PhotoUrl) && !photoUrls.Contains(request.PhotoUrl))
        {
            photoUrls.Insert(0, request.PhotoUrl);
        }

        return photoUrls;
    }
}

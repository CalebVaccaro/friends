using friends.api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace friends.api.Data;

public class CosmosFriendshipRepository : IFriendshipRepository
{
    private const string SwipeDocumentType = "swipe";
    private const string ConnectionDocumentType = "connection";
    private readonly Container _container;

    public CosmosFriendshipRepository(IOptions<FriendsDbOptions> options)
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
        _container = database.CreateContainerIfNotExistsAsync(dbOptions.CosmosFriendshipsContainerName, "/id").GetAwaiter().GetResult().Container;
    }

    public async Task<IReadOnlyList<UserSwipe>> GetSwipesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("""
            SELECT * FROM c
            WHERE c.documentType = @documentType AND c.swiperUserId = @userId
            ORDER BY c.updatedAt DESC
            """)
            .WithParameter("@documentType", SwipeDocumentType)
            .WithParameter("@userId", userId);

        var documents = await ReadAllAsync<SwipeDocument>(query, cancellationToken);

        return documents.Select(document => document.ToSwipe()).ToList();
    }

    public async Task<IReadOnlyList<FriendConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("""
            SELECT * FROM c
            WHERE c.documentType = @documentType AND (c.userAId = @userId OR c.userBId = @userId)
            ORDER BY c.createdAt DESC
            """)
            .WithParameter("@documentType", ConnectionDocumentType)
            .WithParameter("@userId", userId);

        var documents = await ReadAllAsync<ConnectionDocument>(query, cancellationToken);

        return documents.Select(document => document.ToConnection()).ToList();
    }

    public async Task<SwipeResult> SwipeAsync(Guid swiperUserId, SwipeRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var existingSwipe = await GetSwipeDocumentAsync(swiperUserId, request.TargetUserId, cancellationToken);
        var swipeDocument = existingSwipe ?? new SwipeDocument
        {
            Id = Guid.NewGuid().ToString(),
            DocumentType = SwipeDocumentType,
            SwiperUserId = swiperUserId,
            TargetUserId = request.TargetUserId,
            CreatedAt = now
        };

        swipeDocument.Action = request.Action;
        swipeDocument.UpdatedAt = now;

        await _container.UpsertItemAsync(swipeDocument, new PartitionKey(swipeDocument.Id), cancellationToken: cancellationToken);

        var connectionDocument = request.Action == SwipeAction.Like
            ? await CreateConnectionIfMatchedAsync(swiperUserId, request.TargetUserId, now, cancellationToken)
            : null;

        return new SwipeResult
        {
            Swipe = swipeDocument.ToSwipe(),
            IsMatch = connectionDocument is not null,
            Connection = connectionDocument?.ToConnection()
        };
    }

    public async Task<bool> DeleteConnectionAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default)
    {
        var (userAId, userBId) = NormalizeConnection(userId, friendUserId);
        var connection = await GetConnectionDocumentAsync(userAId, userBId, cancellationToken);

        if (connection is null)
        {
            return false;
        }

        await _container.DeleteItemAsync<ConnectionDocument>(connection.Id, new PartitionKey(connection.Id), cancellationToken: cancellationToken);

        return true;
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        using var iterator = _container.GetItemQueryIterator<CosmosDocument>(query);

        while (iterator.HasMoreResults)
        {
            foreach (var document in await iterator.ReadNextAsync(cancellationToken))
            {
                await _container.DeleteItemAsync<CosmosDocument>(document.Id, new PartitionKey(document.Id), cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<ConnectionDocument?> CreateConnectionIfMatchedAsync(Guid swiperUserId, Guid targetUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reciprocalSwipe = await GetSwipeDocumentAsync(targetUserId, swiperUserId, cancellationToken);

        if (reciprocalSwipe?.Action != SwipeAction.Like)
        {
            return null;
        }

        var (userAId, userBId) = NormalizeConnection(swiperUserId, targetUserId);
        var existingConnection = await GetConnectionDocumentAsync(userAId, userBId, cancellationToken);

        if (existingConnection is not null)
        {
            return existingConnection;
        }

        var connection = new ConnectionDocument
        {
            Id = Guid.NewGuid().ToString(),
            DocumentType = ConnectionDocumentType,
            UserAId = userAId,
            UserBId = userBId,
            CreatedAt = now
        };

        await _container.CreateItemAsync(connection, new PartitionKey(connection.Id), cancellationToken: cancellationToken);

        return connection;
    }

    private async Task<SwipeDocument?> GetSwipeDocumentAsync(Guid swiperUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("""
            SELECT * FROM c
            WHERE c.documentType = @documentType
                AND c.swiperUserId = @swiperUserId
                AND c.targetUserId = @targetUserId
            """)
            .WithParameter("@documentType", SwipeDocumentType)
            .WithParameter("@swiperUserId", swiperUserId)
            .WithParameter("@targetUserId", targetUserId);

        return await ReadFirstOrDefaultAsync<SwipeDocument>(query, cancellationToken);
    }

    private async Task<ConnectionDocument?> GetConnectionDocumentAsync(Guid userAId, Guid userBId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("""
            SELECT * FROM c
            WHERE c.documentType = @documentType
                AND c.userAId = @userAId
                AND c.userBId = @userBId
            """)
            .WithParameter("@documentType", ConnectionDocumentType)
            .WithParameter("@userAId", userAId)
            .WithParameter("@userBId", userBId);

        return await ReadFirstOrDefaultAsync<ConnectionDocument>(query, cancellationToken);
    }

    private async Task<T?> ReadFirstOrDefaultAsync<T>(QueryDefinition query, CancellationToken cancellationToken)
    {
        using var iterator = _container.GetItemQueryIterator<T>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var item = response.FirstOrDefault();

            if (item is not null)
            {
                return item;
            }
        }

        return default;
    }

    private async Task<List<T>> ReadAllAsync<T>(QueryDefinition query, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        using var iterator = _container.GetItemQueryIterator<T>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            items.AddRange(response);
        }

        return items;
    }

    private static (Guid UserAId, Guid UserBId) NormalizeConnection(Guid firstUserId, Guid secondUserId)
    {
        return string.CompareOrdinal(firstUserId.ToString(), secondUserId.ToString()) <= 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }

    private class SwipeDocument
    {
        public string Id { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public Guid SwiperUserId { get; set; }
        public Guid TargetUserId { get; set; }
        public SwipeAction Action { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public UserSwipe ToSwipe()
        {
            return new UserSwipe
            {
                Id = Guid.Parse(Id),
                SwiperUserId = SwiperUserId,
                TargetUserId = TargetUserId,
                Action = Action,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }

    private class CosmosDocument
    {
        public string Id { get; set; } = string.Empty;
    }

    private class ConnectionDocument
    {
        public string Id { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public Guid UserAId { get; set; }
        public Guid UserBId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public FriendConnection ToConnection()
        {
            return new FriendConnection
            {
                Id = Guid.Parse(Id),
                UserAId = UserAId,
                UserBId = UserBId,
                CreatedAt = CreatedAt
            };
        }
    }
}

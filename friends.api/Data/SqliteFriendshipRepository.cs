using friends.api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SQLitePCL;

namespace friends.api.Data;

public class SqliteFriendshipRepository : IFriendshipRepository
{
    private readonly string _connectionString;

    public SqliteFriendshipRepository(IOptions<FriendsDbOptions> options)
    {
        Batteries.Init();
        _connectionString = options.Value.SqliteConnectionString;
        EnsureDatabase();
    }

    public async Task<IReadOnlyList<UserSwipe>> GetSwipesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var swipes = new List<UserSwipe>();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT Id, SwiperUserId, TargetUserId, Direction, CreatedAt, UpdatedAt
            FROM Swipes
            WHERE SwiperUserId = @UserId
            ORDER BY UpdatedAt DESC
            """;

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@UserId", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            swipes.Add(ReadSwipe(reader));
        }

        return swipes;
    }

    public async Task<IReadOnlyList<FriendConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connections = new List<FriendConnection>();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT Id, UserAId, UserBId, CreatedAt
            FROM FriendConnections
            WHERE UserAId = @UserId OR UserBId = @UserId
            ORDER BY CreatedAt DESC
            """;

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@UserId", userId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            connections.Add(ReadConnection(reader));
        }

        return connections;
    }

    public async Task<SwipeResult> SwipeAsync(Guid swiperUserId, SwipeRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var existingSwipe = await GetSwipeAsync(connection, swiperUserId, request.TargetUserId, cancellationToken);
        var swipe = existingSwipe ?? new UserSwipe
        {
            Id = Guid.NewGuid(),
            SwiperUserId = swiperUserId,
            TargetUserId = request.TargetUserId,
            CreatedAt = now
        };

        swipe.Action = request.Action;
        swipe.UpdatedAt = now;

        if (existingSwipe is null)
        {
            const string insertSwipe = """
                INSERT INTO Swipes (Id, SwiperUserId, TargetUserId, Direction, CreatedAt, UpdatedAt)
                VALUES (@Id, @SwiperUserId, @TargetUserId, @Direction, @CreatedAt, @UpdatedAt)
                """;

            await using var insertCommand = new SqliteCommand(insertSwipe, connection);
            AddSwipeParameters(insertCommand, swipe);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string updateSwipe = """
                UPDATE Swipes
                SET Direction = @Direction,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id
                """;

            await using var updateCommand = new SqliteCommand(updateSwipe, connection);
            AddSwipeParameters(updateCommand, swipe);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionMatch = request.Action == SwipeAction.Like
            ? await CreateConnectionIfMatchedAsync(connection, swiperUserId, request.TargetUserId, now, cancellationToken)
            : null;

        return new SwipeResult
        {
            Swipe = swipe,
            IsMatch = connectionMatch is not null,
            Connection = connectionMatch
        };
    }

    public async Task<bool> DeleteConnectionAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default)
    {
        var (userAId, userBId) = NormalizeConnection(userId, friendUserId);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string commandText = "DELETE FROM FriendConnections WHERE UserAId = @UserAId AND UserBId = @UserBId";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@UserAId", userAId.ToString());
        command.Parameters.AddWithValue("@UserBId", userBId.ToString());

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var deleteConnections = new SqliteCommand("DELETE FROM FriendConnections", connection);
        await deleteConnections.ExecuteNonQueryAsync(cancellationToken);

        await using var deleteSwipes = new SqliteCommand("DELETE FROM Swipes", connection);
        await deleteSwipes.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private void EnsureDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        const string createSwipes = """
            CREATE TABLE IF NOT EXISTS Swipes (
                Id TEXT PRIMARY KEY,
                SwiperUserId TEXT NOT NULL,
                TargetUserId TEXT NOT NULL,
                Direction TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                UNIQUE (SwiperUserId, TargetUserId)
            )
            """;

        using var createSwipesCommand = new SqliteCommand(createSwipes, connection);
        createSwipesCommand.ExecuteNonQuery();

        const string createConnections = """
            CREATE TABLE IF NOT EXISTS FriendConnections (
                Id TEXT PRIMARY KEY,
                UserAId TEXT NOT NULL,
                UserBId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UNIQUE (UserAId, UserBId)
            )
            """;

        using var createConnectionsCommand = new SqliteCommand(createConnections, connection);
        createConnectionsCommand.ExecuteNonQuery();
    }

    private static async Task<UserSwipe?> GetSwipeAsync(SqliteConnection connection, Guid swiperUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT Id, SwiperUserId, TargetUserId, Direction, CreatedAt, UpdatedAt
            FROM Swipes
            WHERE SwiperUserId = @SwiperUserId AND TargetUserId = @TargetUserId
            """;

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@SwiperUserId", swiperUserId.ToString());
        command.Parameters.AddWithValue("@TargetUserId", targetUserId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadSwipe(reader) : null;
    }

    private static async Task<FriendConnection?> CreateConnectionIfMatchedAsync(SqliteConnection connection, Guid swiperUserId, Guid targetUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reciprocalSwipe = await GetSwipeAsync(connection, targetUserId, swiperUserId, cancellationToken);

        if (reciprocalSwipe?.Action != SwipeAction.Like)
        {
            return null;
        }

        var (userAId, userBId) = NormalizeConnection(swiperUserId, targetUserId);
        var existingConnection = await GetConnectionAsync(connection, userAId, userBId, cancellationToken);

        if (existingConnection is not null)
        {
            return existingConnection;
        }

        var friendConnection = new FriendConnection
        {
            Id = Guid.NewGuid(),
            UserAId = userAId,
            UserBId = userBId,
            CreatedAt = now
        };

        const string commandText = """
            INSERT INTO FriendConnections (Id, UserAId, UserBId, CreatedAt)
            VALUES (@Id, @UserAId, @UserBId, @CreatedAt)
            """;

        await using var command = new SqliteCommand(commandText, connection);
        AddConnectionParameters(command, friendConnection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return friendConnection;
    }

    private static async Task<FriendConnection?> GetConnectionAsync(SqliteConnection connection, Guid userAId, Guid userBId, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT Id, UserAId, UserBId, CreatedAt
            FROM FriendConnections
            WHERE UserAId = @UserAId AND UserBId = @UserBId
            """;

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@UserAId", userAId.ToString());
        command.Parameters.AddWithValue("@UserBId", userBId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadConnection(reader) : null;
    }

    private static void AddSwipeParameters(SqliteCommand command, UserSwipe swipe)
    {
        command.Parameters.AddWithValue("@Id", swipe.Id.ToString());
        command.Parameters.AddWithValue("@SwiperUserId", swipe.SwiperUserId.ToString());
        command.Parameters.AddWithValue("@TargetUserId", swipe.TargetUserId.ToString());
        command.Parameters.AddWithValue("@Direction", swipe.Action.ToString());
        command.Parameters.AddWithValue("@CreatedAt", swipe.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", swipe.UpdatedAt.ToString("O"));
    }

    private static void AddConnectionParameters(SqliteCommand command, FriendConnection connection)
    {
        command.Parameters.AddWithValue("@Id", connection.Id.ToString());
        command.Parameters.AddWithValue("@UserAId", connection.UserAId.ToString());
        command.Parameters.AddWithValue("@UserBId", connection.UserBId.ToString());
        command.Parameters.AddWithValue("@CreatedAt", connection.CreatedAt.ToString("O"));
    }

    private static UserSwipe ReadSwipe(SqliteDataReader reader)
    {
        return new UserSwipe
        {
            Id = Guid.Parse(reader.GetString(0)),
            SwiperUserId = Guid.Parse(reader.GetString(1)),
            TargetUserId = Guid.Parse(reader.GetString(2)),
            Action = Enum.Parse<SwipeAction>(reader.GetString(3)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(4)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(5))
        };
    }

    private static FriendConnection ReadConnection(SqliteDataReader reader)
    {
        return new FriendConnection
        {
            Id = Guid.Parse(reader.GetString(0)),
            UserAId = Guid.Parse(reader.GetString(1)),
            UserBId = Guid.Parse(reader.GetString(2)),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3))
        };
    }

    private static (Guid UserAId, Guid UserBId) NormalizeConnection(Guid firstUserId, Guid secondUserId)
    {
        return string.CompareOrdinal(firstUserId.ToString(), secondUserId.ToString()) <= 0
            ? (firstUserId, secondUserId)
            : (secondUserId, firstUserId);
    }
}

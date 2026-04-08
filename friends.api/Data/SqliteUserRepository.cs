using System.Text.Json;
using friends.api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SQLitePCL;

namespace friends.api.Data;

public class SqliteUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public SqliteUserRepository(IOptions<FriendsDbOptions> options)
    {
        Batteries.Init();
        _connectionString = options.Value.SqliteConnectionString;
        EnsureDatabase();
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = new List<User>();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT Id, Name, Email, Age, Bio, Location, Latitude, Longitude, PhotoUrl, PhotoUrls, Interests, CreatedAt, UpdatedAt
            FROM Users
            ORDER BY CreatedAt DESC
            """;

        await using var command = new SqliteCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT Id, Name, Email, Age, Bio, Location, Latitude, Longitude, PhotoUrl, PhotoUrls, Interests, CreatedAt, UpdatedAt
            FROM Users
            WHERE Id = @Id
            """;

        await using var command = new SqliteCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadUser(reader) : null;
    }

    public async Task<User> CreateAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string commandText = """
            INSERT INTO Users (Id, Name, Email, Age, Bio, Location, Latitude, Longitude, PhotoUrl, PhotoUrls, Interests, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @Email, @Age, @Bio, @Location, @Latitude, @Longitude, @PhotoUrl, @PhotoUrls, @Interests, @CreatedAt, @UpdatedAt)
            """;

        await using var command = new SqliteCommand(commandText, connection);
        AddUserParameters(command, user);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return user;
    }

    public async Task<User?> UpdateAsync(Guid id, UserRequest request, CancellationToken cancellationToken = default)
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

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string commandText = """
            UPDATE Users
            SET Name = @Name,
                Email = @Email,
                Age = @Age,
                Bio = @Bio,
                Location = @Location,
                Latitude = @Latitude,
                Longitude = @Longitude,
                PhotoUrl = @PhotoUrl,
                PhotoUrls = @PhotoUrls,
                Interests = @Interests,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        await using var command = new SqliteCommand(commandText, connection);
        AddUserParameters(command, existing);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string commandText = "DELETE FROM Users WHERE Id = @Id";

        await using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@Id", id.ToString());

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string commandText = "DELETE FROM Users";

        await using var command = new SqliteCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private void EnsureDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        const string commandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL,
                Age INTEGER NOT NULL,
                Bio TEXT NOT NULL,
                Location TEXT NOT NULL,
                Latitude REAL NULL,
                Longitude REAL NULL,
                PhotoUrl TEXT NOT NULL,
                PhotoUrls TEXT NULL,
                Interests TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
            """;

        using var command = new SqliteCommand(commandText, connection);
        command.ExecuteNonQuery();

        EnsureColumn(connection, "Users", "Latitude", "REAL NULL");
        EnsureColumn(connection, "Users", "Longitude", "REAL NULL");
        EnsureColumn(connection, "Users", "PhotoUrls", "TEXT NULL");
    }

    private static void AddUserParameters(SqliteCommand command, User user)
    {
        command.Parameters.AddWithValue("@Id", user.Id.ToString());
        command.Parameters.AddWithValue("@Name", user.Name);
        command.Parameters.AddWithValue("@Email", user.Email);
        command.Parameters.AddWithValue("@Age", user.Age);
        command.Parameters.AddWithValue("@Bio", user.Bio);
        command.Parameters.AddWithValue("@Location", user.LocationLabel);
        command.Parameters.AddWithValue("@Latitude", user.Latitude is null ? DBNull.Value : user.Latitude);
        command.Parameters.AddWithValue("@Longitude", user.Longitude is null ? DBNull.Value : user.Longitude);
        command.Parameters.AddWithValue("@PhotoUrl", user.PhotoUrl);
        command.Parameters.AddWithValue("@PhotoUrls", JsonSerializer.Serialize(user.PhotoUrls));
        command.Parameters.AddWithValue("@Interests", JsonSerializer.Serialize(user.Interests));
        command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@UpdatedAt", user.UpdatedAt.ToString("O"));
    }

    private static User ReadUser(SqliteDataReader reader)
    {
        var user = new User
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Email = reader.GetString(2),
            Age = reader.GetInt32(3),
            Bio = reader.GetString(4),
            LocationLabel = reader.GetString(5),
            Latitude = reader.IsDBNull(6) ? null : reader.GetDouble(6),
            Longitude = reader.IsDBNull(7) ? null : reader.GetDouble(7),
            PhotoUrl = reader.GetString(8),
            PhotoUrls = reader.IsDBNull(9) ? [] : JsonSerializer.Deserialize<List<string>>(reader.GetString(9)) ?? [],
            Interests = JsonSerializer.Deserialize<List<string>>(reader.GetString(10)) ?? [],
            CreatedAt = DateTimeOffset.Parse(reader.GetString(11)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(12))
        };

        if (user.PhotoUrls.Count == 0 && !string.IsNullOrWhiteSpace(user.PhotoUrl))
        {
            user.PhotoUrls = [user.PhotoUrl];
        }

        return user;
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

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var command = new SqliteCommand($"PRAGMA table_info({tableName})", connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alterCommand = new SqliteCommand($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}", connection);
        alterCommand.ExecuteNonQuery();
    }
}

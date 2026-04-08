using System.Text.Json.Serialization;
using friends.api.Data;
using friends.api.Models;
using friends.api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<ILocationService, LocationService>();
builder.Services.AddSingleton<IInterestService, InterestService>();
builder.Services.Configure<FriendsDbOptions>(builder.Configuration.GetSection(FriendsDbOptions.SectionName));
builder.Services.AddSingleton<IUserRepository>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FriendsDbOptions>>().Value;

    return options.Provider.Equals("Cosmos", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<CosmosUserRepository>(serviceProvider)
        : ActivatorUtilities.CreateInstance<SqliteUserRepository>(serviceProvider);
});
builder.Services.AddSingleton<IFriendshipRepository>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<FriendsDbOptions>>().Value;

    return options.Provider.Equals("Cosmos", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<CosmosFriendshipRepository>(serviceProvider)
        : ActivatorUtilities.CreateInstance<SqliteFriendshipRepository>(serviceProvider);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var users = app.MapGroup("/api/users").WithTags("Users");

users.MapGet("/", async (IUserRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.GetAllAsync(cancellationToken)));

users.MapGet("/{id:guid}", async (Guid id, IUserRepository repository, CancellationToken cancellationToken) =>
{
    var user = await repository.GetByIdAsync(id, cancellationToken);

    return user is null ? Results.NotFound() : Results.Ok(user);
});

users.MapGet("/{id:guid}/discover", async (
    Guid id,
    double? radiusMiles,
    string? interests,
    int? limit,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    ILocationService locationService,
    IInterestService interestService,
    CancellationToken cancellationToken) =>
{
    var discoveryResults = await GetDiscoveryResultsAsync(id, radiusMiles, interests, limit, userRepository, friendshipRepository, locationService, interestService, cancellationToken);

    return discoveryResults;
});

users.MapPost("/", async (UserRequest request, IUserRepository repository, CancellationToken cancellationToken) =>
{
    var validationError = Validate(request);

    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var user = await repository.CreateAsync(request, cancellationToken);

    return Results.Created($"/api/users/{user.Id}", user);
});

users.MapPut("/{id:guid}", async (Guid id, UserRequest request, IUserRepository repository, CancellationToken cancellationToken) =>
{
    var validationError = Validate(request);

    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var user = await repository.UpdateAsync(id, request, cancellationToken);

    return user is null ? Results.NotFound() : Results.Ok(user);
});

users.MapDelete("/{id:guid}", async (Guid id, IUserRepository repository, CancellationToken cancellationToken) =>
    await repository.DeleteAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound());

users.MapGet("/{id:guid}/swipes", async (Guid id, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (await userRepository.GetByIdAsync(id, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    return Results.Ok(await friendshipRepository.GetSwipesAsync(id, cancellationToken));
});

users.MapPost("/{id:guid}/swipes", async (Guid id, SwipeRequest request, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    var validationError = await ValidateSwipeAsync(id, request, userRepository, cancellationToken);

    if (validationError is not null)
    {
        return validationError;
    }

    var result = await friendshipRepository.SwipeAsync(id, request, cancellationToken);
    var matchedUser = result.Connection is null
        ? null
        : await userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
    result.MatchedUser = matchedUser is null ? null : ToSummary(matchedUser);

    return Results.Ok(result);
});

users.MapGet("/{id:guid}/connections", async (Guid id, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (await userRepository.GetByIdAsync(id, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    return Results.Ok(await GetConnectionProfilesAsync(id, userRepository, friendshipRepository, cancellationToken));
});

users.MapDelete("/{id:guid}/connections/{friendUserId:guid}", async (Guid id, Guid friendUserId, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (await userRepository.GetByIdAsync(id, cancellationToken) is null ||
        await userRepository.GetByIdAsync(friendUserId, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    return await friendshipRepository.DeleteConnectionAsync(id, friendUserId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound(new { error = "Connection was not found." });
});

var me = app.MapGroup("/api/me").WithTags("Me");

me.MapGet("/", async ([FromHeader(Name = "X-User-Id")] Guid? currentUserId, IUserRepository repository, CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    var user = await repository.GetByIdAsync(currentUserId.Value, cancellationToken);

    return user is null ? Results.NotFound(new { error = "Current user was not found." }) : Results.Ok(user);
});

me.MapPut("/", async ([FromHeader(Name = "X-User-Id")] Guid? currentUserId, UserRequest request, IUserRepository repository, CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    var validationError = Validate(request);

    if (validationError is not null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var user = await repository.UpdateAsync(currentUserId.Value, request, cancellationToken);

    return user is null ? Results.NotFound(new { error = "Current user was not found." }) : Results.Ok(user);
});

me.MapGet("/discover", async (
    [FromHeader(Name = "X-User-Id")] Guid? currentUserId,
    double? radiusMiles,
    string? interests,
    int? limit,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    ILocationService locationService,
    IInterestService interestService,
    CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    return await GetDiscoveryResultsAsync(currentUserId.Value, radiusMiles, interests, limit, userRepository, friendshipRepository, locationService, interestService, cancellationToken);
});

me.MapPost("/swipes", async ([FromHeader(Name = "X-User-Id")] Guid? currentUserId, SwipeRequest request, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    var validationError = await ValidateSwipeAsync(currentUserId.Value, request, userRepository, cancellationToken);

    if (validationError is not null)
    {
        return validationError;
    }

    var result = await friendshipRepository.SwipeAsync(currentUserId.Value, request, cancellationToken);
    var matchedUser = result.Connection is null
        ? null
        : await userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
    result.MatchedUser = matchedUser is null ? null : ToSummary(matchedUser);

    return Results.Ok(result);
});

me.MapGet("/connections", async ([FromHeader(Name = "X-User-Id")] Guid? currentUserId, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    if (await userRepository.GetByIdAsync(currentUserId.Value, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "Current user was not found." });
    }

    return Results.Ok(await GetConnectionProfilesAsync(currentUserId.Value, userRepository, friendshipRepository, cancellationToken));
});

me.MapDelete("/connections/{friendUserId:guid}", async ([FromHeader(Name = "X-User-Id")] Guid? currentUserId, Guid friendUserId, IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    if (currentUserId is null)
    {
        return Results.BadRequest(new { error = "Send X-User-Id with a user id until auth is implemented." });
    }

    if (await userRepository.GetByIdAsync(currentUserId.Value, cancellationToken) is null ||
        await userRepository.GetByIdAsync(friendUserId, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    return await friendshipRepository.DeleteConnectionAsync(currentUserId.Value, friendUserId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound(new { error = "Connection was not found." });
});

var testData = app.MapGroup("/api/test").WithTags("Test Data");

testData.MapPost("/users", async (IUserRepository repository, CancellationToken cancellationToken) =>
{
    var requests = new[]
    {
        new UserRequest
        {
            Name = "Maya",
            Email = $"maya.{Guid.NewGuid():N}@example.com",
            Age = 28,
            Bio = "Coffee walks, board games, and weekend hikes.",
            LocationLabel = "Brooklyn, NY",
            Latitude = 40.6782,
            Longitude = -73.9442,
            PhotoUrl = "https://example.com/maya.jpg",
            PhotoUrls = ["https://example.com/maya.jpg", "https://example.com/maya-2.jpg"],
            Interests = ["hiking", "coffee", "board games"]
        },
        new UserRequest
        {
            Name = "Jordan",
            Email = $"jordan.{Guid.NewGuid():N}@example.com",
            Age = 31,
            Bio = "Looking for bouldering partners and live music friends.",
            LocationLabel = "Queens, NY",
            Latitude = 40.7282,
            Longitude = -73.7949,
            PhotoUrl = "https://example.com/jordan.jpg",
            PhotoUrls = ["https://example.com/jordan.jpg", "https://example.com/jordan-2.jpg"],
            Interests = ["climbing", "hiking", "live music"]
        },
        new UserRequest
        {
            Name = "Priya",
            Email = $"priya.{Guid.NewGuid():N}@example.com",
            Age = 26,
            Bio = "Bookstores, food trucks, and weekend trail runs.",
            LocationLabel = "Jersey City, NJ",
            Latitude = 40.7178,
            Longitude = -74.0431,
            PhotoUrl = "https://example.com/priya.jpg",
            PhotoUrls = ["https://example.com/priya.jpg", "https://example.com/priya-2.jpg"],
            Interests = ["reading", "food trucks", "running", "coffee"]
        },
        new UserRequest
        {
            Name = "Sam",
            Email = $"sam.{Guid.NewGuid():N}@example.com",
            Age = 29,
            Bio = "New in town and always up for trivia night.",
            LocationLabel = "Hoboken, NJ",
            Latitude = 40.7433,
            Longitude = -74.0324,
            PhotoUrl = "https://example.com/sam.jpg",
            PhotoUrls = ["https://example.com/sam.jpg", "https://example.com/sam-2.jpg"],
            Interests = ["trivia", "board games", "craft beer"]
        },
        new UserRequest
        {
            Name = "Alex",
            Email = $"alex.{Guid.NewGuid():N}@example.com",
            Age = 34,
            Bio = "Museum days, park walks, and low-key dinners.",
            LocationLabel = "Manhattan, NY",
            Latitude = 40.7831,
            Longitude = -73.9712,
            PhotoUrl = "https://example.com/alex.jpg",
            PhotoUrls = ["https://example.com/alex.jpg", "https://example.com/alex-2.jpg"],
            Interests = ["museums", "food trucks", "coffee"]
        }
    };

    var users = new List<User>();

    foreach (var request in requests)
    {
        users.Add(await repository.CreateAsync(request, cancellationToken));
    }

    return Results.Created("/api/test/users", users);
});

testData.MapDelete("/data", async (IUserRepository userRepository, IFriendshipRepository friendshipRepository, CancellationToken cancellationToken) =>
{
    await friendshipRepository.DeleteAllAsync(cancellationToken);
    await userRepository.DeleteAllAsync(cancellationToken);

    return Results.NoContent();
});

app.Run();

static string? Validate(UserRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return "Name is required.";
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return "Email is required.";
    }

    if (request.Age < 18)
    {
        return "Users must be at least 18.";
    }

    if (request is { Latitude: not null, Longitude: null } or { Latitude: null, Longitude: not null })
    {
        return "Latitude and longitude must be provided together.";
    }

    if (request.Latitude is < -90 or > 90)
    {
        return "Latitude must be between -90 and 90.";
    }

    if (request.Longitude is < -180 or > 180)
    {
        return "Longitude must be between -180 and 180.";
    }

    return null;
}

static List<string> ParseInterests(string? interests)
{
    return string.IsNullOrWhiteSpace(interests)
        ? []
        : interests
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(interest => !string.IsNullOrWhiteSpace(interest))
            .ToList();
}

static async Task<IResult> GetDiscoveryResultsAsync(
    Guid userId,
    double? radiusMiles,
    string? interests,
    int? limit,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    ILocationService locationService,
    IInterestService interestService,
    CancellationToken cancellationToken)
{
    var user = await userRepository.GetByIdAsync(userId, cancellationToken);

    if (user is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    if (radiusMiles is <= 0)
    {
        return Results.BadRequest(new { error = "Radius must be greater than 0 miles." });
    }

    if (radiusMiles is not null && (user.Latitude is null || user.Longitude is null))
    {
        return Results.BadRequest(new { error = "User needs latitude and longitude to discover by radius." });
    }

    var requestedInterests = ParseInterests(interests);
    var shouldFilterByInterests = requestedInterests.Count > 0 || user.Interests.Count > 0;
    var resultLimit = Math.Clamp(limit ?? 25, 1, 100);
    var candidates = await userRepository.GetAllAsync(cancellationToken);
    var swipedUserIds = (await friendshipRepository.GetSwipesAsync(userId, cancellationToken))
        .Select(swipe => swipe.TargetUserId)
        .ToHashSet();
    var connectedUserIds = (await friendshipRepository.GetConnectionsAsync(userId, cancellationToken))
        .Select(connection => GetOtherUserId(connection, userId))
        .ToHashSet();

    var discoveryResults = candidates
        .Where(candidate => candidate.Id != user.Id)
        .Where(candidate => !swipedUserIds.Contains(candidate.Id))
        .Where(candidate => !connectedUserIds.Contains(candidate.Id))
        .Select(candidate => new UserDiscoveryResult
        {
            User = ToSummary(candidate),
            DistanceMiles = locationService.GetDistanceMiles(user, candidate),
            SharedInterests = interestService.GetSharedInterests(user, candidate, requestedInterests)
        })
        .Where(result => radiusMiles is null || result.DistanceMiles <= radiusMiles)
        .Where(result => !shouldFilterByInterests || result.SharedInterests.Count > 0)
        .OrderByDescending(result => result.SharedInterests.Count)
        .ThenBy(result => result.DistanceMiles ?? double.MaxValue)
        .Take(resultLimit)
        .ToList();

    return Results.Ok(discoveryResults);
}

static async Task<IReadOnlyList<UserProfileSummary>> GetConnectionProfilesAsync(
    Guid userId,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    CancellationToken cancellationToken)
{
    var connections = await friendshipRepository.GetConnectionsAsync(userId, cancellationToken);
    var profiles = new List<UserProfileSummary>();

    foreach (var connection in connections)
    {
        var connectedUser = await userRepository.GetByIdAsync(GetOtherUserId(connection, userId), cancellationToken);

        if (connectedUser is not null)
        {
            profiles.Add(ToSummary(connectedUser));
        }
    }

    return profiles;
}

static Guid GetOtherUserId(FriendConnection connection, Guid userId)
{
    return connection.UserAId == userId ? connection.UserBId : connection.UserAId;
}

static UserProfileSummary ToSummary(User user)
{
    return new UserProfileSummary
    {
        Id = user.Id,
        Name = user.Name,
        Age = user.Age,
        Bio = user.Bio,
        LocationLabel = user.LocationLabel,
        PhotoUrl = user.PhotoUrl,
        PhotoUrls = user.PhotoUrls,
        Interests = user.Interests
    };
}

static async Task<IResult?> ValidateSwipeAsync(Guid userId, SwipeRequest request, IUserRepository userRepository, CancellationToken cancellationToken)
{
    if (!Enum.IsDefined(request.Action))
    {
        return Results.BadRequest(new { error = "Swipe action must be Like or Pass." });
    }

    if (request.TargetUserId == Guid.Empty)
    {
        return Results.BadRequest(new { error = "Target user is required." });
    }

    if (userId == request.TargetUserId)
    {
        return Results.BadRequest(new { error = "Users cannot swipe on themselves." });
    }

    if (await userRepository.GetByIdAsync(userId, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "User was not found." });
    }

    if (await userRepository.GetByIdAsync(request.TargetUserId, cancellationToken) is null)
    {
        return Results.NotFound(new { error = "Target user was not found." });
    }

    return null;
}

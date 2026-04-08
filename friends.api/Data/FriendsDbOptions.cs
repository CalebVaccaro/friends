namespace friends.api.Data;

public class FriendsDbOptions
{
    public const string SectionName = "FriendsDb";

    public string Provider { get; set; } = "Sql";
    public string SqliteConnectionString { get; set; } = "Data Source=friends.db";
    public string CosmosEndpoint { get; set; } = string.Empty;
    public string CosmosKey { get; set; } = string.Empty;
    public string CosmosDatabaseName { get; set; } = "friends";
    public string CosmosContainerName { get; set; } = "Users";
    public string CosmosFriendshipsContainerName { get; set; } = "Friendships";
}

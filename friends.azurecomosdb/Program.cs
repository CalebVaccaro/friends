using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace friends.azurecomosdb;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Friends Azure Cosmos DB");

        IConfiguration configuration = ConfigurationLoader.LoadConfiguration();
        var cosmosClient = await CreateCosmosClientAsync(
            configuration.GetConnectionString("CosmosDbAccountEndpoint") ?? string.Empty,
            configuration.GetConnectionString("CosmosDbAuthKey") ?? string.Empty);

        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync("profiles");
        var friendsContainer = await database.Database.CreateContainerIfNotExistsAsync("Friends", "/id");
        var userContainer = await database.Database.CreateContainerIfNotExistsAsync("Users", "/id");
    }

    static async Task<CosmosClient?> CreateCosmosClientAsync(string accountEndpoint, string authKeyOrResourceToken)
    {
        try
        {
            CosmosClient client = new(accountEndpoint, authKeyOrResourceToken);
            Console.WriteLine("CosmosClient created successfully.");
            return client;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"Error creating CosmosClient: {ex.Message}");
            throw;
        }
    }

    static async Task<Container> CreateContainerIfNotExistsAsync(Database database, string containerName, string partitionKeyPath)
    {
        try
        {
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            return container;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"Error creating container: {ex.Message}");
            throw;
        }
    }

    static async Task<Database?> CreateDatabaseIfNotExistsAsync(CosmosClient client, string databaseName)
    {
        try
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);
            return database;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"Error creating database: {ex.Message}");
            throw;
        }
    }
}
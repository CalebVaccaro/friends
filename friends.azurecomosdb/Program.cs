using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace friends.azurecomosdb;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        IConfiguration configuration = ConfigurationLoader.LoadConfiguration();

        using CosmosClient client = new(
            accountEndpoint: configuration.GetConnectionString("CosmosDbAccountEndpoint"),
            authKeyOrResourceToken: configuration.GetConnectionString("CosmosDbAuthKey"));
        
        var database = await client.CreateDatabaseIfNotExistsAsync("profiles");
        var userContainer = await database.Database.CreateContainerIfNotExistsAsync("Users", "/id");
        var friendsContainer = await database.Database.CreateContainerIfNotExistsAsync("Friends", "/id");
    }
}
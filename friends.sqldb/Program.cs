using Microsoft.Data.Sqlite;
using SQLitePCL;

Batteries.Init();

string databaseName = "Payments";
string databasePath = $"{databaseName}.db";
string connectionString = $"Data Source={databasePath}";

// Ensure the database file does not already exist
if (!File.Exists(databasePath))
{
    File.Create(databasePath);
}

await using (var connection = new SqliteConnection(connectionString))
{
    connection.Open();

    // Create table
    string createTableQuery = @"
                CREATE TABLE Payments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId UNIQUEIDENTIFIER,
                    Amount DECIMAL,
                    Currency TEXT,
                    Token TEXT,
                    Proccessed BOOLEAN
                )";

    await using (var createTableCommand = new SqliteCommand(createTableQuery, connection))
    {
        createTableCommand.ExecuteNonQuery();
        Console.WriteLine($"Table {databaseName} created successfully.");
    }
    
    string fullPath = Path.GetFullPath(databasePath);
    Console.WriteLine($"Database file location: {fullPath}");
    
    // Insert data
    string insertDataQuery = @"
                INSERT INTO Payments (UserId, Amount, Currency, Token, Proccessed)
                VALUES (@UserId, 100.00, 'USD', 'tok_1', true)";
    await using (var insertDataCommand = new SqliteCommand(insertDataQuery, connection))
    {
        insertDataCommand.Parameters.Add("@UserId", SqliteType.Text).Value = Guid.NewGuid().ToString();
        insertDataCommand.ExecuteNonQuery();
        Console.WriteLine("Data inserted successfully.");
    }
}
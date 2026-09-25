using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace PowerMage.Services;

public class SqlLiteService
{
    private readonly string connectionString;

    public SqlLiteService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(connectionString);
    }

    public async Task<SqliteConnection> CreateOpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();

        return connection;
    }


    public Task Initialize()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Device (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Serial TEXT,
                    ProductType TEXT,
                    ProductName TEXT,
                    DisplayName TEXT,
                    IpAddress TEXT,
                    Track INTEGER
                );
            
                CREATE TABLE IF NOT EXISTS DeviceEnergy (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId INTEGER NOT NULL,
                    Timestamp INTEGER NOT NULL,
            
                    WifiSsid TEXT,
                    WifiStrength REAL,
            
                    TotalPowerImportKwh REAL,
                    TotalPowerImportT1Kwh REAL,
                    TotalPowerExportKwh REAL,
                    TotalPowerExportT1Kwh REAL,
            
                    ActivePowerW REAL,
                    ActivePowerL1W REAL,
                    ActiveVoltageV REAL,
                    ActiveCurrentA REAL,
            
                    ActiveReactivePowerVar REAL,
                    ActiveApparentPowerVa REAL,
                    ActivePowerFactor REAL,
                    ActiveFrequencyHz REAL,
            
                    FOREIGN KEY (DeviceId) REFERENCES Device(Id) ON DELETE CASCADE
                );
            
                CREATE INDEX IF NOT EXISTS IX_DeviceEnergy_DeviceId_Timestamp
                    ON DeviceEnergy (DeviceId, Timestamp);
            """;


        return command.ExecuteNonQueryAsync();
    }
}
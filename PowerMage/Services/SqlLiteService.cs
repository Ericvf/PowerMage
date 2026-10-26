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

    public async Task Initialize()
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
                    Track INTEGER,
                    Order INTEGER NOT NULL DEFAULT 0
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
            
                CREATE TABLE IF NOT EXISTS DeviceP1Measurement (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId INTEGER NOT NULL,
                    Timestamp INTEGER NOT NULL,

                    WifiSsid TEXT,
                    WifiStrength REAL,

                    SmrVersion INTEGER,
                    MeterModel TEXT,
                    UniqueId TEXT,
                    ActiveTariff INTEGER,

                    TotalPowerImportKwh REAL,
                    TotalPowerImportT1Kwh REAL,
                    TotalPowerImportT2Kwh REAL,
                    TotalPowerImportT3Kwh REAL,
                    TotalPowerImportT4Kwh REAL,

                    TotalPowerExportKwh REAL,
                    TotalPowerExportT1Kwh REAL,
                    TotalPowerExportT2Kwh REAL,
                    TotalPowerExportT3Kwh REAL,
                    TotalPowerExportT4Kwh REAL,

                    ActivePowerW REAL,
                    ActivePowerL1W REAL,
                    ActivePowerL2W REAL,
                    ActivePowerL3W REAL,

                    ActiveVoltageL1V REAL,
                    ActiveVoltageL2V REAL,
                    ActiveVoltageL3V REAL,

                    ActiveCurrentA REAL,
                    ActiveCurrentL1A REAL,
                    ActiveCurrentL2A REAL,
                    ActiveCurrentL3A REAL,

                    ActiveFrequencyHz REAL,

                    VoltageSagL1Count INTEGER,
                    VoltageSagL2Count INTEGER,
                    VoltageSagL3Count INTEGER,

                    VoltageSwellL1Count INTEGER,
                    VoltageSwellL2Count INTEGER,
                    VoltageSwellL3Count INTEGER,

                    AnyPowerFailCount INTEGER,
                    LongPowerFailCount INTEGER,

                    ActivePowerAverageW REAL,

                    MontlyPowerPeakW REAL,
                    MontlyPowerPeakTimestamp INTEGER,

                    GasUniqueId TEXT,
                    GasType TEXT,
                    GasTimestamp INTEGER,
                    GasValue REAL,
                    GasUnit TEXT,
                    GasTotalM3 REAL,

                    FOREIGN KEY (DeviceId) REFERENCES Device(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS DeviceWaterMeasurement (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId INTEGER NOT NULL,
                    Timestamp INTEGER NOT NULL,

                    WifiSsid TEXT,
                    WifiStrength REAL,

                    TotalLiterM3 REAL,
                    ActiveLiterLpm REAL,
                    TotalLiterOffsetM3 REAL,

                    FOREIGN KEY (DeviceId) REFERENCES Device(Id) ON DELETE CASCADE
                );
            
                CREATE INDEX IF NOT EXISTS IX_DeviceEnergy_DeviceId_Timestamp
                    ON DeviceEnergy (DeviceId, Timestamp);
            """;

        await command.ExecuteNonQueryAsync();


        await using var enphasecommand = connection.CreateCommand();

        enphasecommand.CommandText = """
    
                    CREATE TABLE IF NOT EXISTS EnphaseEnergy (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp INTEGER NOT NULL,
    
                        ProductionPowerW REAL,
                        ConsumptionPowerW REAL,
                        NetConsumptionPowerW REAL,
    
                        TotalProductionKwh REAL,
                        TotalConsumptionKwh REAL,
                        TotalNetConsumptionKwh REAL,
    
                        ProductionL1W REAL,
                        ProductionL2W REAL,
                        ProductionL3W REAL,
    
                        ConsumptionL1W REAL,
                        ConsumptionL2W REAL,
                        ConsumptionL3W REAL,
    
                        NetConsumptionL1W REAL,
                        NetConsumptionL2W REAL,
                        NetConsumptionL3W REAL
                    );
    
                    CREATE INDEX IF NOT EXISTS IX_EnphaseEnergy_Timestamp
                        ON EnphaseEnergy (Timestamp);
        """;

        await enphasecommand.ExecuteNonQueryAsync();

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(Device);";

        var hasThreshold = false;

        using (var reader = checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1) == "Threshold")
                {
                    hasThreshold = true;
                    break;
                }
            }
        }

        if (!hasThreshold)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = """
                ALTER TABLE Device
                ADD COLUMN "Threshold" REAL NOT NULL DEFAULT 0;
                """;

            alterCommand.ExecuteNonQuery();
        }
    }
}
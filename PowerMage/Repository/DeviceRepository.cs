using Dapper;
using PowerMage.Services;

namespace PowerMage.Repository
{
    public class DeviceRepository
    {
        private readonly SqlLiteService sqlLiteService;

        public DeviceRepository(SqlLiteService sqlLiteService)
        {
            this.sqlLiteService = sqlLiteService;
        }

        public async Task<IEnumerable<DeviceModel>> GetDevices()
        {
            using var connection = await sqlLiteService.CreateOpenConnection();

            return await connection.QueryAsync<DeviceModel>(
                """
                    SELECT
                        Id,
                        Serial,
                        ProductType,
                        ProductName,
                        DisplayName,
                        IpAddress,
                        Track
                    FROM Device
                    ORDER BY DisplayName
                """);
        }

        public async Task<DeviceModel> AddDevice(DeviceModel device)
        {
            using var connection = await sqlLiteService.CreateOpenConnection();

            await connection.ExecuteAsync(
                """
                    INSERT INTO Device (Serial, ProductType, ProductName, DisplayName, IpAddress, Track)
                    VALUES (@Serial, @ProductType, @ProductName, @DisplayName, @IpAddress, @Track)
                """, device);

            return device;
        }

        public async Task<IEnumerable<DeviceEnergyAggregate>> GetDeviceEnergyAggregates(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    DeviceId,
                    (Timestamp / 300) * 300 AS IntervalStartUnix,
                    (Timestamp / 300) * 300 + 300 AS IntervalEndUnix,

                    AVG(ActivePowerW) AS AvgActivePowerW,
                    MIN(ActivePowerW) AS MinActivePowerW,
                    MAX(ActivePowerW) AS MaxActivePowerW,

                    AVG(ActivePowerL1W) AS AvgActivePowerL1W,
                    AVG(ActiveVoltageV) AS AvgActiveVoltageV,
                    AVG(ActiveCurrentA) AS AvgActiveCurrentA,
                    AVG(ActivePowerFactor) AS AvgActivePowerFactor,

                    COUNT(ActivePowerW) AS SampleCount

                FROM DeviceEnergy

                WHERE DeviceId = @DeviceId
                  AND Timestamp >= @From
                  AND Timestamp < @To

                GROUP BY
                    DeviceId,
                    (Timestamp / 300)

                ORDER BY IntervalStartUnix;
                """;

            using var connection = await sqlLiteService.CreateOpenConnection();

            var command = new CommandDefinition(
                sql,
                new
                {
                    DeviceId = deviceId,
                    From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                    To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds()
                },
                cancellationToken: cancellationToken);

            return await connection.QueryAsync<DeviceEnergyAggregate>(command);
        }

        public async Task<double> GetTotalPowerImportKwh(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            using var connection = await sqlLiteService.CreateOpenConnection();
            var totalKwh = await connection.ExecuteScalarAsync<double?>(
                """
                    SELECT
                        MAX(TotalPowerImportKwh) - MIN(TotalPowerImportKwh)
                    FROM DeviceEnergy
                    WHERE DeviceId = @DeviceId
                      AND Timestamp >= @From
                      AND Timestamp < @To;
                """,
                new
                {
                    DeviceId = deviceId,
                    From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                    To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds()
                });

            return totalKwh ?? 0;
        }

        public async Task<DeviceEnergyModel> AddDeviceEnergy(DeviceEnergyModel energy)
        {
            using var connection = await sqlLiteService.CreateOpenConnection();

            await connection.ExecuteAsync(
                """
                INSERT INTO DeviceEnergy (
                    DeviceId,
                    Timestamp,
                    WifiSsid,
                    WifiStrength,
                    TotalPowerImportKwh,
                    TotalPowerImportT1Kwh,
                    TotalPowerExportKwh,
                    TotalPowerExportT1Kwh,
                    ActivePowerW,
                    ActivePowerL1W,
                    ActiveVoltageV,
                    ActiveCurrentA,
                    ActiveReactivePowerVar,
                    ActiveApparentPowerVa,
                    ActivePowerFactor,
                    ActiveFrequencyHz
                )
                VALUES (
                    @DeviceId,
                    @Timestamp,
                    @WifiSsid,
                    @WifiStrength,
                    @TotalPowerImportKwh,
                    @TotalPowerImportT1Kwh,
                    @TotalPowerExportKwh,
                    @TotalPowerExportT1Kwh,
                    @ActivePowerW,
                    @ActivePowerL1W,
                    @ActiveVoltageV,
                    @ActiveCurrentA,
                    @ActiveReactivePowerVar,
                    @ActiveApparentPowerVa,
                    @ActivePowerFactor,
                    @ActiveFrequencyHz
                );
        """,
                energy);

            return energy;
        }
    }

    public class DeviceEnergyModel
    {
        public long Id { get; set; }

        public long DeviceId { get; set; }

        public long Timestamp { get; set; }

        public string? WifiSsid { get; set; }

        public double? WifiStrength { get; set; }

        public double? TotalPowerImportKwh { get; set; }
        public double? TotalPowerImportT1Kwh { get; set; }

        public double? TotalPowerExportKwh { get; set; }
        public double? TotalPowerExportT1Kwh { get; set; }

        public double? ActivePowerW { get; set; }
        public double? ActivePowerL1W { get; set; }

        public double? ActiveVoltageV { get; set; }
        public double? ActiveCurrentA { get; set; }

        public double? ActiveReactivePowerVar { get; set; }
        public double? ActiveApparentPowerVa { get; set; }
        public double? ActivePowerFactor { get; set; }
        public double? ActiveFrequencyHz { get; set; }
    }
    public class DeviceEnergyAggregate
    {
        public int DeviceId { get; set; }

        public long IntervalStartUnix { get; set; }
        public long IntervalEndUnix { get; set; }

        public double? AvgActivePowerW { get; set; }
        public double? MinActivePowerW { get; set; }
        public double? MaxActivePowerW { get; set; }

        public double? AvgActivePowerL1W { get; set; }
        public double? AvgActiveVoltageV { get; set; }
        public double? AvgActiveCurrentA { get; set; }
        public double? AvgActivePowerFactor { get; set; }

        public int SampleCount { get; set; }

        public DateTime IntervalStart => DateTimeOffset.FromUnixTimeSeconds(IntervalStartUnix).UtcDateTime;
    }


    public class DeviceModel
    {
        public int Id { get; set; }
        public required string Serial { get; set; }
        public required string ProductType { get; set; }
        public required string ProductName { get; set; }
        public required string DisplayName { get; set; }
        public required string IpAddress { get; set; }
        public int Track { get; set; } = 0;
    }
}

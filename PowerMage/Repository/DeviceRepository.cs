using Dapper;
using PowerMage.Services;

namespace PowerMage.Repository;

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

        var devices = await connection.QueryAsync<DeviceModel>(
            """
                SELECT
                    Id,
                    Serial,
                    ProductType,
                    ProductName,
                    DisplayName,
                    IpAddress,
                    Track, 
                    [Order]

                FROM Device
                ORDER BY [Order], DisplayName
            """);

        int no = 1;
        foreach (var device in devices)
        {
            device.Order = no++;
        }

        return devices;
    }

    public async Task UpdateDeviceOrder(int firstDeviceId,int firstOrder,int secondDeviceId,int secondOrder)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            """
            UPDATE Device
            SET [Order] = CASE
                WHEN Id = @FirstId THEN @FirstOrder
                WHEN Id = @SecondId THEN @SecondOrder
            END
            WHERE Id IN (@FirstId, @SecondId);
            """,
            new
            {
                FirstId = firstDeviceId,
                FirstOrder = firstOrder,
                SecondId = secondDeviceId,
                SecondOrder = secondOrder
            },
            transaction);

        transaction.Commit();
    }

    public async Task<DeviceModel> AddDevice(DeviceModel device)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        await connection.ExecuteAsync(
            """
                INSERT INTO Device (Serial, ProductType, ProductName, DisplayName, IpAddress, Track, [Order])
                VALUES (@Serial, @ProductType, @ProductName, @DisplayName, @IpAddress, @Track, @Order)
            """, device);

        return device;
    }

    public async Task<IEnumerable<DeviceSKTEnergyAggregate>> GetSKTDeviceEnergyAggregates(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                DeviceId,
                @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
                @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,

                
            AVG(ActivePowerW) AS AvgActivePowerW

            FROM DeviceEnergy

            WHERE DeviceId = @DeviceId
              AND Timestamp >= @From
              AND Timestamp < @To

            GROUP BY
                DeviceId,
                (Timestamp - @From) / @TimeUnit

            ORDER BY IntervalStartUnix;
            """;

        using var connection = await sqlLiteService.CreateOpenConnection();

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceSKTEnergyAggregate>(command);
    }

    public async Task<IEnumerable<DeviceSKTEnergyAggregate>> GetSKTDeviceEnergyKwhAggregates(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                DeviceId,
                @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
                @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,

                
              (COALESCE(MAX(TotalPowerImportKwh) - MIN(TotalPowerImportKwh), 0) 
              - 
              COALESCE(MAX(TotalPowerExportKwh) - MIN(TotalPowerExportKwh), 0) )
                  AS AvgActivePowerW

            FROM DeviceEnergy

            WHERE DeviceId = @DeviceId
              AND Timestamp >= @From
              AND Timestamp < @To

            GROUP BY
                DeviceId,
                (Timestamp - @From) / @TimeUnit

            ORDER BY IntervalStartUnix;
            """;

        using var connection = await sqlLiteService.CreateOpenConnection();

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceSKTEnergyAggregate>(command);
    }

    public async Task<IEnumerable<DeviceP1EnergyAggregate>> GetDeviceP1EnergyAggregates(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        const string sql = """
            SELECT
                DeviceId,

                @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
                @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,

                AVG(ActivePowerW) AS AvgActivePowerW

            FROM DeviceP1Measurement

            WHERE DeviceId = @DeviceId
                AND Timestamp >= @From
                AND Timestamp < @To

            GROUP BY
                DeviceId,
                (Timestamp - @From) / @TimeUnit

            ORDER BY
                IntervalStartUnix;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceP1EnergyAggregate>(command);
    }

    public async Task<IEnumerable<DeviceP1EnergyAggregateImportExport>> GetDeviceP1EnergyAggregatesImportExport(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        const string sql = """
            SELECT
                DeviceId,

                @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
                @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,

            COALESCE(
                MAX(TotalPowerImportKwh) - MIN(TotalPowerImportKwh),
                0
            ) AS ImportKwh,
            
            COALESCE(
                MAX(TotalPowerExportKwh) - MIN(TotalPowerExportKwh),
                0
            ) AS ExportKwh

            FROM DeviceP1Measurement

            WHERE DeviceId = @DeviceId
                AND Timestamp >= @From
                AND Timestamp < @To

            GROUP BY
                DeviceId,
                (Timestamp - @From) / @TimeUnit

            ORDER BY
                IntervalStartUnix;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceP1EnergyAggregateImportExport>(command);
    }

    public async Task<IEnumerable<DeviceP1GasAggregate>> GetDeviceP1GasAggregates(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        const string sql = """
                SELECT
                    @DeviceId AS DeviceId,
            
                    @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
                    @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,
            
                    MAX(GasValue) - MIN(GasValue) AS GasConsumptionM3
            
                FROM DeviceP1Measurement
                WHERE DeviceId = @DeviceId
                  AND Timestamp >= @From
                  AND Timestamp < @To
                  AND GasValue IS NOT NULL
            
                GROUP BY (Timestamp - @From) / @TimeUnit
                ORDER BY IntervalStartUnix;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceP1GasAggregate>(command);
    }

    public async Task<IEnumerable<DeviceWaterAggregate>> GetDeviceWaterAggregates(int deviceId, DateTime from, DateTime to, int timeUnit = 300, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        const string sql = """
        SELECT
            @DeviceId AS DeviceId,

            @From + (((Timestamp - @From) / @TimeUnit) * @TimeUnit) AS IntervalStartUnix,
            @From + (((Timestamp - @From) / @TimeUnit + 1) * @TimeUnit) AS IntervalEndUnix,

            MAX(TotalLiterM3) - MIN(TotalLiterM3) AS WaterConsumptionM3

        FROM DeviceWaterMeasurement
        WHERE DeviceId = @DeviceId
          AND Timestamp >= @From
          AND Timestamp < @To
          AND TotalLiterM3 IS NOT NULL

        GROUP BY
            (Timestamp - @From) / @TimeUnit

        ORDER BY IntervalStartUnix;
        """;

        var command = new CommandDefinition(
            sql,
            new
            {
                DeviceId = deviceId,
                From = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds(),
                TimeUnit = timeUnit
            },
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<DeviceWaterAggregate>(command);
    }

    public async Task<double> GetTotalPowerSKT(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
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

    public async Task<double> GetTotalP1(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();
        var totalKwh = await connection.ExecuteScalarAsync<double?>(
            """
                SELECT
                    MAX(TotalPowerImportKwh) - MIN(TotalPowerImportKwh)
                FROM DeviceP1Measurement
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

    public async Task<double> GetTotalExportP1(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();
        var totalKwh = await connection.ExecuteScalarAsync<double?>(
            """
                SELECT
                    MAX(TotalPowerExportKwh) - MIN(TotalPowerExportKwh)
                FROM DeviceP1Measurement
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

    public async Task<double> GetTotalGasConsumed(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();
        var totalGas = await connection.ExecuteScalarAsync<double?>(
            """
            SELECT
                    COALESCE(MAX(GasValue) - MIN(GasValue), 0)
                FROM DeviceP1Measurement
                WHERE DeviceId = @DeviceId
                  AND Timestamp >= @From
                  AND Timestamp < @To
                  AND GasValue IS NOT NULL;
            """,
            new
            {
                DeviceId = deviceId,
                From = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeSeconds()
            });

        return totalGas ?? 0;
    }

    public async Task<double> GetTotalWaterConsumed(int deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        var totalWater = await connection.ExecuteScalarAsync<double?>(
            """
                SELECT
                    COALESCE(MAX(TotalLiterM3) - MIN(TotalLiterM3), 0)
                FROM DeviceWaterMeasurement
                WHERE DeviceId = @DeviceId
                  AND Timestamp >= @From
                  AND Timestamp < @To
                  AND TotalLiterM3 IS NOT NULL;
            """,
            new
            {
                DeviceId = deviceId,
                From = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeSeconds(),
                To = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeSeconds()
            });

        return totalWater ?? 0;
    }

    public async Task<DeviceEnergyModel> AddSKTDeviceEnergy(DeviceEnergyModel energy)
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

    public async Task<DeviceP1EnergyModel> AddP1DeviceEnergy(DeviceP1EnergyModel energy)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        await connection.ExecuteAsync(
            """
    INSERT INTO DeviceP1Measurement (
        DeviceId,
        Timestamp,

        WifiSsid,
        WifiStrength,

        SmrVersion,
        MeterModel,
        UniqueId,
        ActiveTariff,

        TotalPowerImportKwh,
        TotalPowerImportT1Kwh,
        TotalPowerImportT2Kwh,
        TotalPowerImportT3Kwh,
        TotalPowerImportT4Kwh,

        TotalPowerExportKwh,
        TotalPowerExportT1Kwh,
        TotalPowerExportT2Kwh,
        TotalPowerExportT3Kwh,
        TotalPowerExportT4Kwh,

        ActivePowerW,
        ActivePowerL1W,
        ActivePowerL2W,
        ActivePowerL3W,

        ActiveVoltageL1V,
        ActiveVoltageL2V,
        ActiveVoltageL3V,

        ActiveCurrentA,
        ActiveCurrentL1A,
        ActiveCurrentL2A,
        ActiveCurrentL3A,

        ActiveFrequencyHz,

        VoltageSagL1Count,
        VoltageSagL2Count,
        VoltageSagL3Count,

        VoltageSwellL1Count,
        VoltageSwellL2Count,
        VoltageSwellL3Count,

        AnyPowerFailCount,
        LongPowerFailCount,

        ActivePowerAverageW,

        MontlyPowerPeakW,
        MontlyPowerPeakTimestamp,

        GasUniqueId,
        GasType,
        GasTimestamp,
        GasValue,
        GasUnit,
        GasTotalM3
    )
    VALUES (
        @DeviceId,
        @Timestamp,

        @WifiSsid,
        @WifiStrength,

        @SmrVersion,
        @MeterModel,
        @UniqueId,
        @ActiveTariff,

        @TotalPowerImportKwh,
        @TotalPowerImportT1Kwh,
        @TotalPowerImportT2Kwh,
        @TotalPowerImportT3Kwh,
        @TotalPowerImportT4Kwh,

        @TotalPowerExportKwh,
        @TotalPowerExportT1Kwh,
        @TotalPowerExportT2Kwh,
        @TotalPowerExportT3Kwh,
        @TotalPowerExportT4Kwh,

        @ActivePowerW,
        @ActivePowerL1W,
        @ActivePowerL2W,
        @ActivePowerL3W,

        @ActiveVoltageL1V,
        @ActiveVoltageL2V,
        @ActiveVoltageL3V,

        @ActiveCurrentA,
        @ActiveCurrentL1A,
        @ActiveCurrentL2A,
        @ActiveCurrentL3A,

        @ActiveFrequencyHz,

        @VoltageSagL1Count,
        @VoltageSagL2Count,
        @VoltageSagL3Count,

        @VoltageSwellL1Count,
        @VoltageSwellL2Count,
        @VoltageSwellL3Count,

        @AnyPowerFailCount,
        @LongPowerFailCount,

        @ActivePowerAverageW,

        @MontlyPowerPeakW,
        @MontlyPowerPeakTimestamp,

        @GasUniqueId,
        @GasType,
        @GasTimestamp,
        @GasValue,
        @GasUnit,
        @TotalGasM3
    );
    """,
            new
            {
                energy.DeviceId,
                energy.Timestamp,

                energy.WifiSsid,
                energy.WifiStrength,

                energy.SmrVersion,
                energy.MeterModel,
                energy.UniqueId,
                energy.ActiveTariff,

                energy.TotalPowerImportKwh,
                energy.TotalPowerImportT1Kwh,
                energy.TotalPowerImportT2Kwh,
                energy.TotalPowerImportT3Kwh,
                energy.TotalPowerImportT4Kwh,

                energy.TotalPowerExportKwh,
                energy.TotalPowerExportT1Kwh,
                energy.TotalPowerExportT2Kwh,
                energy.TotalPowerExportT3Kwh,
                energy.TotalPowerExportT4Kwh,

                energy.ActivePowerW,
                energy.ActivePowerL1W,
                energy.ActivePowerL2W,
                energy.ActivePowerL3W,

                energy.ActiveVoltageL1V,
                energy.ActiveVoltageL2V,
                energy.ActiveVoltageL3V,

                energy.ActiveCurrentA,
                energy.ActiveCurrentL1A,
                energy.ActiveCurrentL2A,
                energy.ActiveCurrentL3A,

                energy.ActiveFrequencyHz,

                energy.VoltageSagL1Count,
                energy.VoltageSagL2Count,
                energy.VoltageSagL3Count,

                energy.VoltageSwellL1Count,
                energy.VoltageSwellL2Count,
                energy.VoltageSwellL3Count,

                energy.AnyPowerFailCount,
                energy.LongPowerFailCount,

                energy.ActivePowerAverageW,

                energy.MontlyPowerPeakW,
                energy.MontlyPowerPeakTimestamp,

                GasUniqueId = energy.External?.FirstOrDefault()?.UniqueId,
                GasType = energy.External?.FirstOrDefault()?.Type,
                GasTimestamp = energy.External?.FirstOrDefault()?.Timestamp,
                GasValue = energy.External?.FirstOrDefault()?.Value,
                GasUnit = energy.External?.FirstOrDefault()?.Unit,
                energy.TotalGasM3
            });

        return energy;
    }

    public async Task<DeviceWaterMeasurementModel> AddDeviceWater(DeviceWaterMeasurementModel water)
    {
        using var connection = await sqlLiteService.CreateOpenConnection();

        await connection.ExecuteAsync(
            """
            INSERT INTO DeviceWaterMeasurement (
                DeviceId,
                Timestamp,
                WifiSsid,
                WifiStrength,
                TotalLiterM3,
                ActiveLiterLpm,
                TotalLiterOffsetM3
            )
            VALUES (
                @DeviceId,
                @Timestamp,
                @WifiSsid,
                @WifiStrength,
                @TotalLiterM3,
                @ActiveLiterLpm,
                @TotalLiterOffsetM3
            );
            """,
            water);

        return water;
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

public class DeviceP1EnergyAggregate
{
    public long DeviceId { get; set; }

    public long IntervalStartUnix { get; set; }
    public long IntervalEndUnix { get; set; }

    public double? AvgActivePowerW { get; set; }

    public DateTime IntervalStart => DateTimeOffset.FromUnixTimeSeconds(IntervalStartUnix).UtcDateTime;

    public DateTime IntervalEnd => DateTimeOffset.FromUnixTimeSeconds(IntervalEndUnix).UtcDateTime;
}

public class DeviceP1EnergyAggregateImportExport
{
    public long DeviceId { get; set; }

    public long IntervalStartUnix { get; set; }
    public long IntervalEndUnix { get; set; }

    public double? ImportKwh { get; set; }
    public double? ExportKwh { get; set; }

    public DateTime IntervalStart => DateTimeOffset.FromUnixTimeSeconds(IntervalStartUnix).UtcDateTime;

    public DateTime IntervalEnd => DateTimeOffset.FromUnixTimeSeconds(IntervalEndUnix).UtcDateTime;
}

public class DeviceP1GasAggregate
{
    public long DeviceId { get; set; }

    public long IntervalStartUnix { get; set; }
    public long IntervalEndUnix { get; set; }

    public double? LastGasValue { get; set; }
    public double? PreviousGasValue { get; set; }
    public double? GasConsumptionM3 { get; set; }

    public DateTime IntervalStart => DateTimeOffset.FromUnixTimeSeconds(IntervalStartUnix).UtcDateTime;

    public DateTime IntervalEnd => DateTimeOffset.FromUnixTimeSeconds(IntervalEndUnix).UtcDateTime;
}

public class DeviceSKTEnergyAggregate
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

    public DateTime IntervalEnd => DateTimeOffset.FromUnixTimeSeconds(IntervalEndUnix).UtcDateTime;
}

public class DeviceWaterAggregate
{
    public long DeviceId { get; set; }

    public long IntervalStartUnix { get; set; }
    public long IntervalEndUnix { get; set; }

    public double? LastTotalLiterM3 { get; set; }
    public double? PreviousTotalLiterM3 { get; set; }

    public double? ActiveLiterLpm { get; set; }
    public double? TotalLiterOffsetM3 { get; set; }

    public double? WaterConsumptionM3 { get; set; }

    public DateTime IntervalStart =>
        DateTimeOffset.FromUnixTimeSeconds(IntervalStartUnix).UtcDateTime;

    public DateTime IntervalEnd =>
        DateTimeOffset.FromUnixTimeSeconds(IntervalEndUnix).UtcDateTime;
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
    public int Order { get; set; } = 0;
}

public class DeviceP1EnergyModel
{
    public long Id { get; set; }

    public long DeviceId { get; set; }

    public long Timestamp { get; set; }

    public string? WifiSsid { get; set; }
    public double? WifiStrength { get; set; }

    public int? SmrVersion { get; set; }
    public string? MeterModel { get; set; }
    public string? UniqueId { get; set; }
    public int? ActiveTariff { get; set; }

    public double? TotalPowerImportKwh { get; set; }
    public double? TotalPowerImportT1Kwh { get; set; }
    public double? TotalPowerImportT2Kwh { get; set; }
    public double? TotalPowerImportT3Kwh { get; set; }
    public double? TotalPowerImportT4Kwh { get; set; }

    public double? TotalPowerExportKwh { get; set; }
    public double? TotalPowerExportT1Kwh { get; set; }
    public double? TotalPowerExportT2Kwh { get; set; }
    public double? TotalPowerExportT3Kwh { get; set; }
    public double? TotalPowerExportT4Kwh { get; set; }

    public double? ActivePowerW { get; set; }
    public double? ActivePowerL1W { get; set; }
    public double? ActivePowerL2W { get; set; }
    public double? ActivePowerL3W { get; set; }

    public double? ActiveVoltageL1V { get; set; }
    public double? ActiveVoltageL2V { get; set; }
    public double? ActiveVoltageL3V { get; set; }

    public double? ActiveCurrentA { get; set; }
    public double? ActiveCurrentL1A { get; set; }
    public double? ActiveCurrentL2A { get; set; }
    public double? ActiveCurrentL3A { get; set; }

    public double? ActiveFrequencyHz { get; set; }

    public int? VoltageSagL1Count { get; set; }
    public int? VoltageSagL2Count { get; set; }
    public int? VoltageSagL3Count { get; set; }

    public int? VoltageSwellL1Count { get; set; }
    public int? VoltageSwellL2Count { get; set; }
    public int? VoltageSwellL3Count { get; set; }

    public int? AnyPowerFailCount { get; set; }
    public int? LongPowerFailCount { get; set; }

    public double? ActivePowerAverageW { get; set; }

    public double? MontlyPowerPeakW { get; set; }
    public long? MontlyPowerPeakTimestamp { get; set; }

    public double? TotalGasM3 { get; set; }
    public long? GasTimestamp { get; set; }
    public string? GasUniqueId { get; set; }

    public List<P1ExternalMeasurementModel>? External { get; set; }
}

public class P1ExternalMeasurementModel
{
    public string? UniqueId { get; set; }

    public string? Type { get; set; }

    public long? Timestamp { get; set; }

    public double? Value { get; set; }

    public string? Unit { get; set; }
}

public class DeviceWaterMeasurementModel
{
    public long Id { get; set; }
    public long DeviceId { get; set; }
    public long Timestamp { get; set; }

    public string? WifiSsid { get; set; }
    public double? WifiStrength { get; set; }

    public double? TotalLiterM3 { get; set; }
    public double? ActiveLiterLpm { get; set; }
    public double? TotalLiterOffsetM3 { get; set; }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerMage.Api;
using PowerMage.Repository;

namespace PowerMage;

public class CollectorHostedService(DeviceRepository deviceRepository, DeviceClient deviceClient, EnphaseClient enphaseClient, ILogger<CollectorHostedService> logger, IConfiguration configuration) : BackgroundService
{
    private const int MaxRetryAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hasEnphaseConfiguration = configuration.GetSection("Enphase").Exists();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var devices = await deviceRepository.GetDevices();

                var tasks = devices
                    .Where(d => d.Track == 1)
                    .Select(device => RunDeviceCollectorAsync(device, stoppingToken))
                    .ToList();

                if (hasEnphaseConfiguration)
                {
                    var enphaseTask = RunEnphaseEnergy(stoppingToken);
                    tasks.Add(enphaseTask);
                }

                await Task.WhenAll(tasks.ToArray());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting device collectors");
            }
        }
    }

    private async Task RunDeviceCollectorAsync(DeviceModel device, CancellationToken stoppingToken)
    {
        var interval = GetDeviceInterval(device);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectDeviceWithRetryAsync(device, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect measurements for device {DisplayName}", device.DisplayName);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CollectDeviceWithRetryAsync(DeviceModel device, CancellationToken stoppingToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await CollectDeviceAsync(device, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} failed for device {DisplayName}", attempt, MaxRetryAttempts, device.DisplayName);
                if (attempt < MaxRetryAttempts)
                {
                    continue;
                }
            }
        }

        throw new Exception($"Failed to collect device {device.DisplayName} after {MaxRetryAttempts} attempts.", lastException);
    }

    private async Task CollectDeviceAsync(DeviceModel device, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            logger.LogWarning("Device {DisplayName} has no IP address", device.DisplayName);

            return;
        }

        logger.LogInformation("Reading device: {DisplayName} ({IpAddress})", device.DisplayName, device.IpAddress);

        switch (device.ProductType)
        {
            case "HWE-SKT":
                await CollectSKTAsync(device, stoppingToken);
                break;

            case "HWE-P1":
                await CollectP1Async(device, stoppingToken);
                break;

            case "HWE-WTR":
                await CollectWaterAsync(device, stoppingToken);
                break;

            default:
                logger.LogWarning("Unsupported product type {ProductType} for device {DisplayName}", device.ProductType, device.DisplayName);
                break;
        }
    }

    private async Task RunEnphaseEnergy(CancellationToken stoppingToken)
    {
        var interval = 30000;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var productionDetails = await enphaseClient.GetEnphaseProductionDetails(stoppingToken);
                if (productionDetails is null)
                {
                    throw new InvalidOperationException($"No measurement returned by Enphase");
                }

                await deviceRepository.AddEnphaseEnergy(productionDetails);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect measurements for Enphase");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CollectSKTAsync(DeviceModel device, CancellationToken stoppingToken)
    {
        var measurement = await deviceClient.GetSKTMeasurement(device, stoppingToken);
        if (measurement is null)
        {
            throw new InvalidOperationException($"No SKT measurement returned by {device.DisplayName}");
        }

        await deviceRepository.AddSKTDeviceEnergy(measurement);
    }

    private async Task CollectP1Async(DeviceModel device, CancellationToken stoppingToken)
    {
        var measurement = await deviceClient.GetP1Measurement(device, stoppingToken);
        if (measurement is null)
        {
            throw new InvalidOperationException($"No P1 measurement returned by {device.DisplayName}");
        }

        await deviceRepository.AddP1DeviceEnergy(measurement);
    }

    private async Task CollectWaterAsync(DeviceModel device, CancellationToken stoppingToken)
    {
        var measurement = await deviceClient.GetWaterMeasurement(device, stoppingToken);
        if (measurement is null)
        {
            throw new InvalidOperationException($"No water measurement returned by {device.DisplayName}");
        }

        await deviceRepository.AddDeviceWater(measurement);
    }

    private static TimeSpan GetDeviceInterval(DeviceModel device)
    {
        return TimeSpan.FromSeconds(30);
    }
}

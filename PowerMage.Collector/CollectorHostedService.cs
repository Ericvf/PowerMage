using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerMage.Api;
using PowerMage.Repository;

namespace PowerMage;

public class CollectorHostedService(DeviceRepository deviceRepository, DeviceClient deviceClient, ILogger<CollectorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var devices = await deviceRepository.GetDevices();

                foreach (var device in devices.Where(d => d.Track == 1))
                {
                    logger.LogInformation("Reading device: {DisplayName} ({IpAddress})", device.DisplayName, device.IpAddress);

                    if (string.IsNullOrWhiteSpace(device.IpAddress))
                    {
                        logger.LogWarning("Device {DisplayName} has no IP address", device.DisplayName);
                        continue;
                    }

                    if (device.ProductType == "HWE-SKT")
                    {
                        var measurement = await deviceClient.GetSKTMeasurement(device, stoppingToken);
                        if (measurement is null)
                        {
                            logger.LogWarning("No measurement returned by {DisplayName}", device.DisplayName);
                            continue;
                        }

                        await deviceRepository.AddSKTDeviceEnergy(measurement);
                    }
                    else if (device.ProductType == "HWE-P1")
                    {
                        var measurement = await deviceClient.GetP1Measurement(device, stoppingToken);
                        if (measurement is null)
                        {
                            logger.LogWarning("No measurement returned by {DisplayName}", device.DisplayName);
                            continue;
                        }
                        await deviceRepository.AddP1DeviceEnergy(measurement);

                    }
                    else if (device.ProductType == "HWE-WTR")
                    {
                        var measurement = await deviceClient.GetWaterMeasurement(device, stoppingToken);
                        if (measurement is null)
                        {
                            logger.LogWarning("No measurement returned by {DisplayName}", device.DisplayName);
                            continue;
                        }

                        await deviceRepository.AddDeviceWater(measurement);
                    }
                    else
                    {
                        logger.LogWarning("Unsupported product type {ProductType} for device {DisplayName}", device.ProductType, device.DisplayName);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while collecting device measurements");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}


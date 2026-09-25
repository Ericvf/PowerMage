using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerMage.Api;
using PowerMage.Repository;

namespace PowerMage;

public sealed class CollectorHostedService(DeviceRepository deviceRepository, DeviceClient deviceClient, ILogger<CollectorHostedService> logger) : BackgroundService
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

                    var measurement = await deviceClient.GetMeasurement(device, stoppingToken);
                    if (measurement is null)
                    {
                        logger.LogWarning("No measurement returned by {DisplayName}", device.DisplayName);
                        continue;
                    }

                    await deviceRepository.AddDeviceEnergy(measurement);


                    //if (device.Id == 1)
                    //{
                    //    foreach (var deviceEnergy in data)
                    //    {
                    //        Console.WriteLine($"Device: {device.DisplayName}, Interval: {DateTimeOffset.FromUnixTimeSeconds(deviceEnergy.IntervalEndUnix).UtcDateTime.ToLocalTime()}, Avg Power: {deviceEnergy.AvgActivePowerW}, SampleCount: {deviceEnergy.SampleCount}");
                    //    }
                    //}
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
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}


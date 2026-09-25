using Microsoft.Extensions.DependencyInjection;
using PowerMage.Api;
using PowerMage.Repository;
using PowerMage.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPowerMage(
        this IServiceCollection services)
    {
        services
            .AddSingleton<SqlLiteService>()
            .AddSingleton<DeviceRepository>()
            .AddSingleton<IHomewizardDiscovery, HomewizardDiscovery>();

        services.AddHttpClient<DeviceClient>();

        return services;
    }
}
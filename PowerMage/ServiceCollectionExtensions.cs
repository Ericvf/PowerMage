using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PowerMage.Api;
using PowerMage.Repository;
using PowerMage.Services;
using System.Net.Http.Headers;

namespace PowerMage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPowerMage(this IServiceCollection services)
    {
        services
            .AddSingleton<SqlLiteService>()
                .AddSingleton<DeviceRepository>()
                .AddSingleton<IHomewizardDiscovery, HomewizardDiscovery>();

        services.AddHttpClient<DeviceClient>();
        services.AddHttpClient<EnphaseClient>((serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var token = configuration.GetValue<string>("Enphase:Token");
            var baseUrl = configuration.GetValue<string>("Enphase:BaseUrl");

            if (token != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (baseUrl != null)
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => true
        });

        return services;
    }
}
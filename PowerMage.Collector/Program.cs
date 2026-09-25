using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerMage;
using PowerMage.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
});

builder.Services.AddPowerMage();
builder.Services.AddHostedService<CollectorHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sqliteService = scope.ServiceProvider.GetRequiredService<SqlLiteService>();

    using var connection = sqliteService.CreateConnection();
    connection.Open();

    await sqliteService.Initialize();
}

await app.RunAsync();

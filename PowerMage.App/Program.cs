using PowerMage.Components;
using PowerMage.Repository;
using PowerMage.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
});

builder.Services.AddPowerMage();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var sqliteService = scope.ServiceProvider.GetRequiredService<SqlLiteService>();

    using var connection = sqliteService.CreateConnection();
    connection.Open();

    await sqliteService.Initialize();
}

await app.RunAsync();
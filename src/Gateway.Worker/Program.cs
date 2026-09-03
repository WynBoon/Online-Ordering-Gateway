using Azure.Monitor.OpenTelemetry.Exporter;
using Gateway.Adapters.Gaap;
using Gateway.Adapters.OrderHarmony;
using Gateway.Adapters.Pilot;
using Gateway.Application.UseCases;
using Gateway.Infrastructure.DependencyInjection;
using Gateway.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

// The async worker (ARCHITECTURE.md §8): everything reading off Service Bus —
// webhook delivery/retry, GAAP status synthesis, scheduled menu re-pull. Runs
// as Azure Functions on Flex Consumption rather than a hand-rolled
// BackgroundService, so drain-on-deploy and PeekLock complete/abandon come
// from the platform (ARCHITECTURE.md §14).
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddOrderHarmonyChannel();
builder.Services.AddGaapAdapter(builder.Configuration);
builder.Services.AddPilotAdapter(builder.Configuration);
builder.Services.AddScoped<StatusSyncUseCase>();
builder.Services.AddScoped<MenuSyncUseCase>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var host = builder.Build();
await DevelopmentHost.InitializeAsync(
    host.Services,
    host.Services.GetRequiredService<IConfiguration>(),
    host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment());
host.Run();

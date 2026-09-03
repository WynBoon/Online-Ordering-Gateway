using Gateway.Adapters.Gaap;
using Gateway.Adapters.OrderHarmony;
using Gateway.Adapters.Pilot;
using Gateway.Application.UseCases;
using Gateway.Infrastructure.DependencyInjection;
using Gateway.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// The Order Harmony-facing surface — App Service, min 1 instance, hard timeouts
// (10s injection, 30s menu pull) mean this stays always-warm rather than
// scale-to-zero. See docs/architecture/ARCHITECTURE.md §8.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGatewayInfrastructure(builder.Configuration);
builder.Services.AddOrderHarmonyChannel();
builder.Services.AddGaapAdapter(builder.Configuration);
builder.Services.AddPilotAdapter(builder.Configuration);

builder.Services.AddScoped<OrderInjectionUseCase>();
builder.Services.AddScoped<MenuSyncUseCase>();
builder.Services.AddScoped<HealthCheckUseCase>();
builder.Services.AddScoped<StatusSyncUseCase>();

var app = builder.Build();

await DevelopmentHost.InitializeAsync(app.Services, app.Configuration, app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed for Gateway.Api.CertificationTests via WebApplicationFactory<Program>.
public partial class Program;

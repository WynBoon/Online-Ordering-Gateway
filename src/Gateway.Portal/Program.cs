using Gateway.Adapters.Gaap;
using Gateway.Adapters.Pilot;
using Gateway.Infrastructure.DependencyInjection;
using Gateway.Portal.Components;
using Gateway.Portal.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// A separate App Service Plan from Gateway.Api by design — an internal Blazor
// app with stateful circuits doesn't belong sharing compute with the
// latency-sensitive, partner-facing API (ARCHITECTURE.md §8, UI-ARCHITECTURE.md).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Internal-only: Microsoft Entra ID gives Authenticator MFA and Conditional
// Access without building any of it ourselves (ARCHITECTURE.md §13).
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});
builder.Services.AddCascadingAuthenticationState();

// Direct DB access per the decided architecture — no internal API layer
// between the portal and its own database (UI-ARCHITECTURE.md, "Decided").
builder.Services.AddGatewayInfrastructure(builder.Configuration);

// Needed for the onboarding wizard's "test connection" step, which calls the
// POS health adapters directly (UI-ARCHITECTURE.md, decision 6).
builder.Services.AddGaapAdapter(builder.Configuration);
builder.Services.AddPilotAdapter(builder.Configuration);

// Live feed: Service Bus topic subscription fanned out in-process to connected
// Blazor circuits — no Azure SignalR Service backplane while the portal runs
// as a single instance (UI-ARCHITECTURE.md, "Decided").
builder.Services.AddSingleton<LiveOrderFeedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveOrderFeedService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

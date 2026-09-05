using Microsoft.Extensions.Logging;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IDeviceSessionStore, SecureStorageSessionStore>();
        builder.Services.AddSingleton<OrderSelection>();
        builder.Services.AddHttpClient<GatewayDeviceClient>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<BoardViewModel>();
        builder.Services.AddTransient<Pages.OnboardingPage>();
        builder.Services.AddTransient<Pages.BoardPage>();
        builder.Services.AddTransient<Pages.OrderPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

using Microsoft.Extensions.Logging;
using Ordering.Client;
using Ordering.ViewModels;

namespace Ordering.App;

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

        builder.Services.AddSingleton<IOrderingSessionStore, SecureStorageOrderingSessionStore>();
        builder.Services.AddSingleton<MenuSelection>();
        builder.Services.AddSingleton<TrackingSelection>();
        builder.Services.AddSingleton<LabContext>();
        builder.Services.AddSingleton<CartViewModel>();
        builder.Services.AddHttpClient<IOrderingClient, ChannelOrderingClient>();
        builder.Services.AddTransient<ConnectViewModel>();
        builder.Services.AddTransient<MenuViewModel>();
        builder.Services.AddTransient<ItemViewModel>();
        builder.Services.AddTransient<TrackingViewModel>();
        builder.Services.AddTransient<OrdersViewModel>();
        builder.Services.AddTransient<LabViewModel>();
        builder.Services.AddTransient<Pages.ConnectPage>();
        builder.Services.AddTransient<Pages.MenuPage>();
        builder.Services.AddTransient<Pages.ItemPage>();
        builder.Services.AddTransient<Pages.CartPage>();
        builder.Services.AddTransient<Pages.OrdersPage>();
        builder.Services.AddTransient<Pages.TrackingPage>();
        builder.Services.AddTransient<Pages.LabPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

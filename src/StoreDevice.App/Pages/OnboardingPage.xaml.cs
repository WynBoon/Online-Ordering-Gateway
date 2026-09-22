using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App.Pages;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _vm;
    private readonly IDeviceSessionStore _sessions;

    public OnboardingPage()
        : this(
            Resolve<OnboardingViewModel>(),
            Resolve<IDeviceSessionStore>())
    {
    }

    public OnboardingPage(OnboardingViewModel vm, IDeviceSessionStore sessions)
    {
        InitializeComponent();
        _vm = vm;
        _sessions = sessions;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _sessions.GetAsync() is not null)
        {
            await Shell.Current.GoToAsync("//board");
        }
    }

    private async void OnPairClicked(object? sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            _vm.Status = "Pairing did not start.";
            _vm.Error =
                "This tablet has no internet. Cold-boot the Android emulator, and in Extended controls → Settings → Proxy leave it off (do not use 127.0.0.1).";
            return;
        }

        var paired = await _vm.PairAsync();
        if (paired)
        {
            await Task.Delay(600);
            await Shell.Current.GoToAsync("//board");
        }
    }

    private static T Resolve<T>() where T : notnull
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        return services.GetRequiredService<T>();
    }
}

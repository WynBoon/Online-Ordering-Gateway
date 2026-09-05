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
        try
        {
            await _vm.PairAsync();
            await Shell.Current.GoToAsync("//board");
        }
        catch
        {
            // Error is bound onto the page.
        }
    }

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current?.Services.GetRequiredService<T>()
        ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
}

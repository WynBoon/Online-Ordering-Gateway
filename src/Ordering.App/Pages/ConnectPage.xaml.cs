using Ordering.Client;
using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class ConnectPage : ContentPage
{
    private readonly ConnectViewModel _vm;
    private readonly IOrderingSessionStore _sessions;

    public ConnectPage()
        : this(PageServices.Resolve<ConnectViewModel>(), PageServices.Resolve<IOrderingSessionStore>())
    {
    }

    public ConnectPage(ConnectViewModel vm, IOrderingSessionStore sessions)
    {
        InitializeComponent();
        _vm = vm;
        _sessions = sessions;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var session = await _sessions.GetAsync();
        _vm.GatewayBaseUrl = session is null || GatewayEndpoints.IsLocal(session.GatewayBaseUrl)
            ? GatewayEndpoints.UatApi
            : session.GatewayBaseUrl;
        if (session is not null)
        {
            _vm.LocationKey = session.LocationKey;
        }
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        try
        {
            await _vm.ConnectAsync();
            if (!string.IsNullOrWhiteSpace(_vm.Error) && string.IsNullOrWhiteSpace(_vm.Health))
            {
                return;
            }

            await Shell.Current.GoToAsync("//menu");
        }
        catch
        {
            // Error is bound onto the page.
        }
    }
}

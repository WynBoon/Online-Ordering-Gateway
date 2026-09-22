using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App.Pages;

public partial class BoardPage : ContentPage
{
    private readonly BoardViewModel _vm;
    private readonly OrderSelection _selection;
    private IDispatcherTimer? _tick;
    private IDispatcherTimer? _poll;
    private IDispatcherTimer? _snack;

    public BoardPage()
        : this(Resolve<BoardViewModel>(), Resolve<OrderSelection>())
    {
    }

    public BoardPage(BoardViewModel vm, OrderSelection selection)
    {
        InitializeComponent();
        _vm = vm;
        _selection = selection;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.OpenDetails += OnOpenDetailsAsync;
        await LoadBoardAsync(showBusy: true);
        StartTimers();
    }

    protected override void OnDisappearing()
    {
        _vm.OpenDetails -= OnOpenDetailsAsync;
        StopTimers();
        base.OnDisappearing();
    }

    private void StartTimers()
    {
        _tick ??= Dispatcher.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick -= OnTick;
        _tick.Tick += OnTick;
        _tick.Start();

        _poll ??= Dispatcher.CreateTimer();
        _poll.Interval = TimeSpan.FromSeconds(5);
        _poll.Tick -= OnPoll;
        _poll.Tick += OnPoll;
        _poll.Start();
    }

    private void StopTimers()
    {
        _tick?.Stop();
        _poll?.Stop();
        _snack?.Stop();
    }

    private void OnTick(object? sender, EventArgs e) => _vm.Tick(DateTimeOffset.UtcNow);

    private async void OnPoll(object? sender, EventArgs e) =>
        await LoadBoardAsync(showBusy: false);

    private async Task LoadBoardAsync(bool showBusy)
    {
        await _vm.LoadAsync(CancellationToken.None, showBusy);
        if (_vm.Session is null)
        {
            await Shell.Current.GoToAsync("//onboarding");
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) =>
        await LoadBoardAsync(showBusy: true);

    private async void OnUnpairClicked(object? sender, EventArgs e)
    {
        await _vm.UnpairAsync();
        await Shell.Current.GoToAsync("//onboarding");
    }

    private Task OnOpenDetailsAsync(TicketViewModel ticket)
    {
        _selection.Current = ticket.Order;
        return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("order"));
    }

    private static T Resolve<T>() where T : notnull
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        return services.GetRequiredService<T>();
    }
}

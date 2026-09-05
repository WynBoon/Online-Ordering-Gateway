using Gateway.Domain.Devices;
using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App.Pages;

public partial class BoardPage : ContentPage
{
    private readonly BoardViewModel _vm;
    private readonly OrderSelection _selection;

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
        await LoadBoardAsync();
    }

    private async Task LoadBoardAsync()
    {
        await _vm.LoadAsync();
        if (_vm.Session is null)
        {
            await Shell.Current.GoToAsync("//onboarding");
            return;
        }

        StoreLabel.Text = _vm.StoreName;
        FunctionsLabel.Text = string.Join(" · ", GrantedTitles(_vm.Functions));
        ErrorLabel.Text = _vm.Error ?? "";
        OrdersView.ItemsSource = _vm.Orders;
        OrdersView.SelectedItem = null;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadBoardAsync();

    private async void OnUnpairClicked(object? sender, EventArgs e)
    {
        await _vm.UnpairAsync();
        await Shell.Current.GoToAsync("//onboarding");
    }

    private async void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not DeviceOrderDto order)
        {
            return;
        }

        _selection.Current = order;
        await Shell.Current.GoToAsync("order");
    }

    private static IEnumerable<string> GrantedTitles(DeviceFunction functions) =>
        DeviceFunctionCatalog.All.Where(d => functions.HasFlag(d.Flag)).Select(d => d.Title);

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current?.Services.GetRequiredService<T>()
        ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
}

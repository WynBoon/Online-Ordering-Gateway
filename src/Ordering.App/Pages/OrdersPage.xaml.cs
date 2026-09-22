using Ordering.Client;
using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersViewModel _vm;
    private readonly TrackingSelection _tracking;

    public OrdersPage()
        : this(PageServices.Resolve<OrdersViewModel>(), PageServices.Resolve<TrackingSelection>())
    {
    }

    public OrdersPage(OrdersViewModel vm, TrackingSelection tracking)
    {
        InitializeComponent();
        _vm = vm;
        _tracking = tracking;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await _vm.LoadAsync();
        ErrorLabel.Text = _vm.Error ?? "";
        OrdersView.ItemsSource = _vm.Orders;
        OrdersView.SelectedItem = null;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ChannelOrderDto order)
        {
            return;
        }

        _tracking.OrderRef = order.OrderRef;
        await Shell.Current.GoToAsync("tracking");
    }
}

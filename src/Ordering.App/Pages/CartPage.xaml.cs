using Ordering.Cart;
using Ordering.Client;
using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;
    private readonly TrackingSelection _tracking;

    public CartPage()
        : this(PageServices.Resolve<CartViewModel>(), PageServices.Resolve<TrackingSelection>())
    {
    }

    public CartPage(CartViewModel vm, TrackingSelection tracking)
    {
        InitializeComponent();
        _vm = vm;
        _tracking = tracking;
        BindingContext = _vm;
        FulfillmentPicker.ItemsSource = _vm.FulfillmentOptions.ToList();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshTotals();
        LinesView.ItemsSource = null;
        LinesView.ItemsSource = _vm.Cart.Lines.ToList();
        TotalsLabel.Text = $"{_vm.ItemCountLabel} · {_vm.TotalLabel}";
        NameEntry.Text = _vm.CustomerName;
        PhoneEntry.Text = _vm.CustomerPhone;
        NotesEntry.Text = _vm.Notes;
        AddressEntry.Text = _vm.DeliveryLine1;
        FulfillmentPicker.SelectedItem = _vm.FulfillmentType;
        DeliveryBlock.IsVisible = _vm.IsDelivery;
        ErrorLabel.Text = _vm.Error ?? "";
    }

    private void OnFulfillmentChanged(object? sender, EventArgs e)
    {
        if (FulfillmentPicker.SelectedItem is string value)
        {
            _vm.FulfillmentType = value;
            DeliveryBlock.IsVisible = _vm.IsDelivery;
        }
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is CartLine line)
        {
            _vm.RemoveLine(line);
            OnAppearing();
        }
    }

    private async void OnPlaceClicked(object? sender, EventArgs e)
    {
        _vm.CustomerName = NameEntry.Text ?? "";
        _vm.CustomerPhone = PhoneEntry.Text ?? "";
        _vm.Notes = NotesEntry.Text ?? "";
        _vm.DeliveryLine1 = AddressEntry.Text ?? "";

        var result = await _vm.PlaceAsync();
        ErrorLabel.Text = _vm.Error ?? "";
        OnAppearing();
        if (result is { Success: true })
        {
            _tracking.OrderRef = result.OrderRef;
            _tracking.Placed = new ChannelOrderDto
            {
                OrderRef = result.OrderRef,
                DisplayId = result.DisplayId,
                Status = "accepted",
                PosOrderId = result.PosOrderId,
                FulfillmentType = _vm.FulfillmentType,
                TotalCents = 0,
                Currency = "ZAR",
                PlacedAt = DateTimeOffset.UtcNow
            };
            await Shell.Current.GoToAsync("tracking");
        }
    }
}

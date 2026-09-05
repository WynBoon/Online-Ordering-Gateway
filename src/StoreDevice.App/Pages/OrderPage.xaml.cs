using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App.Pages;

public partial class OrderPage : ContentPage
{
    private readonly BoardViewModel _vm;
    private readonly OrderSelection _selection;

    public OrderPage()
        : this(Resolve<BoardViewModel>(), Resolve<OrderSelection>())
    {
    }

    public OrderPage(BoardViewModel vm, OrderSelection selection)
    {
        InitializeComponent();
        _vm = vm;
        _selection = selection;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        var order = _selection.Current;
        if (order is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = $"#{order.DisplayId}  {order.Status}";
        MetaLabel.Text = $"{order.CustomerName} · {order.Fulfillment} · {order.SourceChannel}";
        NotesLabel.IsVisible = !string.IsNullOrWhiteSpace(order.Notes);
        NotesLabel.Text = order.Notes ?? "";

        LinesStack.Children.Clear();
        foreach (var item in order.Items)
        {
            var modifiers = item.Modifiers.Count == 0 ? "" : $" ({string.Join(", ", item.Modifiers)})";
            LinesStack.Children.Add(new Label
            {
                Text = $"{item.Quantity} × {item.Name}{modifiers}",
                TextColor = Colors.White,
                FontSize = 18
            });
            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                LinesStack.Children.Add(new Label
                {
                    Text = item.Notes,
                    TextColor = Color.FromArgb("#F59E0B"),
                    FontSize = 14
                });
            }
        }

        PreparingButton.IsVisible = _vm.Can(DeviceFunction.MarkPreparing);
        ReadyButton.IsVisible = _vm.Can(DeviceFunction.MarkReady);
        CompletedButton.IsVisible = _vm.Can(DeviceFunction.MarkCompleted);
        DelayButton.IsVisible = _vm.Can(DeviceFunction.AdjustPromiseTime);
        CancelButton.IsVisible = _vm.Can(DeviceFunction.CancelOrder);
        ErrorLabel.Text = "";
    }

    private async void OnPreparing(object? sender, EventArgs e) =>
        await RunAsync(order => _vm.MarkPreparingAsync(order.OrderRef));

    private async void OnReady(object? sender, EventArgs e) =>
        await RunAsync(order => _vm.MarkReadyAsync(order.OrderRef));

    private async void OnCompleted(object? sender, EventArgs e) =>
        await RunAsync(order => _vm.MarkCompletedAsync(order.OrderRef));

    private async void OnDelay(object? sender, EventArgs e) =>
        await RunAsync(order => _vm.DelayAsync(order.OrderRef, 5));

    private async void OnCancel(object? sender, EventArgs e) =>
        await RunAsync(order => _vm.CancelAsync(order.OrderRef, CancelReason.MerchantRejected));

    private async Task RunAsync(Func<DeviceOrderDto, Task> action)
    {
        var order = _selection.Current;
        if (order is null)
        {
            return;
        }

        try
        {
            ErrorLabel.Text = "";
            await action(order);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
        }
    }

    private static T Resolve<T>() where T : notnull =>
        IPlatformApplication.Current?.Services.GetRequiredService<T>()
        ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
}

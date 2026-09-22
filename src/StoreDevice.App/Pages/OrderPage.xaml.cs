using Gateway.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using StoreDevice.Client;
using StoreDevice.ViewModels;

namespace StoreDevice.App.Pages;

public partial class OrderPage : ContentPage
{
    private readonly BoardViewModel _vm;
    private readonly OrderSelection _selection;
    private TicketViewModel? _ticket;

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
        await _vm.LoadAsync(CancellationToken.None, showBusy: false);
        var order = _selection.Current;
        if (order is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _ticket = new TicketViewModel(order, _vm.Functions, _ => Task.CompletedTask, _ => Task.CompletedTask);
        BindTicket(_ticket);
        ErrorLabel.Text = "";
    }

    private void BindTicket(TicketViewModel ticket)
    {
        NumberLabel.Text = $"#{ticket.DisplayNumber}";
        IdentityLabel.Text = $"{ticket.Identity} · {ticket.Order.SourceChannel}";
        FulfillmentLabel.Text = ticket.FulfillmentLabel;
        TimerLabel.Text = ticket.TimerText;
        SyncLabel.Text = ticket.SyncText;
        NoteBox.IsVisible = ticket.HasNote;
        NotesLabel.Text = ticket.Order.Notes ?? "";

        LinesStack.Children.Clear();
        foreach (var item in ticket.Order.Items)
        {
            var line = new TicketLine(
                item.Quantity.ToString(),
                item.Name,
                item.Modifiers.Count == 0 ? null : string.Join("  ", item.Modifiers.Select(m => "+ " + m)),
                item.Notes,
                item.Modifiers.Count > 0,
                !string.IsNullOrWhiteSpace(item.Notes));

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(new GridLength(44)),
                    new ColumnDefinition(GridLength.Star)
                ]
            };
            grid.Add(new Label
            {
                Text = line.Quantity,
                TextColor = Color.FromArgb("#F3F6FA"),
                FontSize = 22,
                FontFamily = "OpenSansSemibold"
            }, 0, 0);
            grid.Add(BuildLineBody(line), 1, 0);
            LinesStack.Children.Add(grid);
        }

        PrimaryButton.Text = ticket.PrimaryText;
        PrimaryButton.BackgroundColor = Color.FromArgb(ticket.PrimaryColor);
        PrimaryButton.IsVisible = ticket.CanPrimary;
        DelayButton.IsVisible = _vm.Can(Gateway.Domain.Devices.DeviceFunction.AdjustPromiseTime);
        CancelButton.IsVisible = _vm.Can(Gateway.Domain.Devices.DeviceFunction.CancelOrder);
    }

    private static VerticalStackLayout BuildLineBody(TicketLine line)
    {
        var stack = new VerticalStackLayout();
        stack.Children.Add(new Label { Text = line.Name, TextColor = Color.FromArgb("#F3F6FA"), FontSize = 20 });
        if (line.HasModifiers)
        {
            stack.Children.Add(new Label { Text = line.Modifiers, TextColor = Color.FromArgb("#B39DFF"), FontSize = 16 });
        }

        if (line.HasNotes)
        {
            stack.Children.Add(new Label { Text = line.Notes, TextColor = Color.FromArgb("#FFB454"), FontSize = 16 });
        }

        return stack;
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnPrimary(object? sender, EventArgs e)
    {
        if (_ticket is null)
        {
            return;
        }

        await RunAsync(() => _vm.RunPrimaryAsync(_ticket));
    }

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

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            ErrorLabel.Text = "";
            await action();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
        }
    }

    private static T Resolve<T>() where T : notnull
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        return services.GetRequiredService<T>();
    }
}

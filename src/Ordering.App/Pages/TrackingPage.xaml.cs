using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class TrackingPage : ContentPage
{
    private readonly TrackingViewModel _vm;
    private readonly TrackingSelection _selection;
    private IDispatcherTimer? _timer;
    private string? _orderRef;

    public TrackingPage()
        : this(PageServices.Resolve<TrackingViewModel>(), PageServices.Resolve<TrackingSelection>())
    {
    }

    public TrackingPage(TrackingViewModel vm, TrackingSelection selection)
    {
        InitializeComponent();
        _vm = vm;
        _selection = selection;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _orderRef = _selection.OrderRef;
        if (string.IsNullOrWhiteSpace(_orderRef))
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_selection.Placed is not null)
        {
            _vm.Seed(_selection.Placed);
            ApplyUi();
        }

        await RefreshAsync();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(2);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_timer is not null)
        {
            _timer.Tick -= OnTick;
            _timer.Stop();
            _timer = null;
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_vm.IsTerminal)
        {
            _timer?.Stop();
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_orderRef))
        {
            return;
        }

        await _vm.LoadAsync(_orderRef);
        ApplyUi();
    }

    private void ApplyUi()
    {
        HeadlineLabel.Text = _vm.Headline;
        StatusLabel.Text = _vm.StatusLabel;
        LadderLabel.Text = _vm.LadderHint;
        ErrorLabel.Text = _vm.Error ?? "";

        var order = _vm.Order;
        if (order is null)
        {
            return;
        }

        MetaLabel.Text = $"{order.FulfillmentType} · pos {order.PosOrderId ?? "—"}";
        ItemsStack.Children.Clear();
        foreach (var item in order.Items)
        {
            var extras = item.Modifiers.Count == 0 ? "" : $" ({string.Join(", ", item.Modifiers)})";
            ItemsStack.Children.Add(new Label
            {
                Text = $"{item.Quantity} × {item.Name}{extras}",
                TextColor = Color.FromArgb("#0F172A"),
                FontSize = 16
            });
        }

        EventsStack.Children.Clear();
        foreach (var evt in order.Events ?? [])
        {
            EventsStack.Children.Add(new Label
            {
                Text = $"{evt.Status} · {evt.EventTime.ToLocalTime():HH:mm:ss}",
                TextColor = Color.FromArgb("#64748B"),
                FontSize = 14
            });
        }
    }
}

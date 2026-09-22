using System.Globalization;
using System.Windows.Input;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using StoreDevice.Client;

namespace StoreDevice.ViewModels;

public sealed class RelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class TicketViewModel : ObservableViewModel
{
    public const int MaxBoardLines = 6;

    private string _timerText = "";
    private bool _busy;

    public TicketViewModel(
        DeviceOrderDto order,
        DeviceFunction functions,
        Func<TicketViewModel, Task> onPrimary,
        Func<TicketViewModel, Task> onOpen)
    {
        Order = order;
        Functions = functions;
        PrimaryCommand = new RelayCommand(() => onPrimary(this), () => CanPrimary && !Busy);
        OpenCommand = new RelayCommand(() => onOpen(this));
        RefreshTimer(DateTimeOffset.UtcNow);
    }

    public DeviceOrderDto Order { get; }

    public DeviceFunction Functions { get; }

    public ICommand PrimaryCommand { get; }

    public ICommand OpenCommand { get; }

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
            {
                Raise(nameof(CanTapPrimary));
                ((RelayCommand)PrimaryCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string DisplayNumber => FormatDisplayNumber(Order.DisplayId, Order.OrderRef);

    public string Identity => FormatIdentity(Order.CustomerName);

    public string FulfillmentLabel => Order.Fulfillment switch
    {
        nameof(FulfillmentType.Pickup) or "Collect" or "Collection" => "Collection",
        nameof(FulfillmentType.DineIn) => "Dine-in",
        nameof(FulfillmentType.Delivery) => "Delivery",
        _ => string.IsNullOrWhiteSpace(Order.Fulfillment) ? "Collection" : Order.Fulfillment
    };

    public string ColumnKey => Order.Status switch
    {
        OrderStatus.Preparing => "prep",
        OrderStatus.Ready => "ready",
        _ => "new"
    };

    public string StripeColor => IsLate ? "#FF6B6B" : Order.Status switch
    {
        OrderStatus.Preparing => "#B39DFF",
        OrderStatus.Ready => "#4ADE80",
        _ => "#5AA9FF"
    };

    public string PrimaryColor => StripeColor;

    public string PrimaryText => Order.Status switch
    {
        OrderStatus.Preparing => "Mark ready",
        OrderStatus.Ready => "Handed over",
        _ => PromiseLabel is { } promise ? $"Accept · {promise}" : "Accept"
    };

    public bool CanPrimary => Order.Status switch
    {
        OrderStatus.Preparing => Functions.HasFlag(DeviceFunction.MarkReady),
        OrderStatus.Ready => Functions.HasFlag(DeviceFunction.MarkCompleted),
        _ => Functions.HasFlag(DeviceFunction.MarkPreparing)
    };

    public string? PromiseLabel =>
        Order.ScheduledForUtc is { } when
            ? when.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
            : null;

    public IReadOnlyList<TicketLine> VisibleLines =>
        Order.Items.Take(MaxBoardLines).Select(ToLine).ToList();

    public string? MoreItemsText =>
        Order.Items.Count > MaxBoardLines ? $"+{Order.Items.Count - MaxBoardLines} more items" : null;

    public bool HasMoreItems => MoreItemsText is not null;

    public bool HasNote => !string.IsNullOrWhiteSpace(Order.Notes);

    public bool CanTapPrimary => CanPrimary && !Busy;

    public string SyncText => string.IsNullOrWhiteSpace(Order.PosOrderId) ? "Sending to POS…" : "In POS";

    public string TimerText
    {
        get => _timerText;
        private set => Set(ref _timerText, value);
    }

    public bool IsLate { get; private set; }

    public string TimerChipColor => IsLate ? "#FF6B6B" : StripeColor;

    public void RefreshTimer(DateTimeOffset utcNow)
    {
        IsLate = ComputeLate(utcNow);
        TimerText = ComputeTimer(utcNow);
        Raise(nameof(IsLate));
        Raise(nameof(TimerChipColor));
        Raise(nameof(StripeColor));
        Raise(nameof(PrimaryColor));
    }

    private bool ComputeLate(DateTimeOffset utcNow) => Order.Status switch
    {
        OrderStatus.Accepted => utcNow - Order.PlacedAtUtc > TimeSpan.FromSeconds(180),
        OrderStatus.Preparing when Order.ScheduledForUtc is { } promise => utcNow > promise,
        OrderStatus.Preparing => utcNow - Order.PlacedAtUtc > TimeSpan.FromMinutes(15),
        OrderStatus.Ready => utcNow - (Order.ScheduledForUtc ?? Order.PlacedAtUtc) > TimeSpan.FromMinutes(20),
        _ => false
    };

    private string ComputeTimer(DateTimeOffset utcNow)
    {
        switch (Order.Status)
        {
            case OrderStatus.Preparing when Order.ScheduledForUtc is { } promise:
                var remain = promise - utcNow;
                return remain < TimeSpan.Zero ? "+" + FormatDuration(-remain) : FormatDuration(remain);
            case OrderStatus.Ready:
                return "waiting " + FormatDuration(utcNow - (Order.ScheduledForUtc ?? Order.PlacedAtUtc));
            default:
                return FormatDuration(utcNow - Order.PlacedAtUtc);
        }
    }

    public static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = -span;
        }

        if (span.TotalSeconds < 60)
        {
            return $"{(int)span.TotalSeconds}s";
        }

        if (span.TotalMinutes < 60)
        {
            return $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
        }

        return $"{(int)span.TotalHours}:{span.Minutes:D2}";
    }

    public static string FormatDisplayNumber(string displayId, string orderRef)
    {
        var digits = new string((displayId ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length >= 4)
        {
            return digits[^4..];
        }

        if (digits.Length > 0)
        {
            return digits;
        }

        var fromRef = new string((orderRef ?? "").Where(char.IsDigit).ToArray());
        return fromRef.Length >= 4 ? fromRef[^4..] : displayId ?? orderRef ?? "";
    }

    public static string FormatIdentity(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Guest";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0];
        }

        return $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}.";
    }

    private static TicketLine ToLine(DeviceOrderItemDto item) =>
        new(
            item.Quantity.ToString(),
            item.Name,
            item.Modifiers.Count == 0 ? null : string.Join("  ", item.Modifiers.Select(m => "+ " + m)),
            item.Notes,
            item.Modifiers.Count > 0,
            !string.IsNullOrWhiteSpace(item.Notes));
}

public sealed record TicketLine(
    string Quantity,
    string Name,
    string? Modifiers,
    string? Notes,
    bool HasModifiers,
    bool HasNotes);


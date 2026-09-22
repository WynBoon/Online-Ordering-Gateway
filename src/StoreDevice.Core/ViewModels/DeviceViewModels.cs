using System.ComponentModel;
using System.Runtime.CompilerServices;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using StoreDevice.Client;

namespace StoreDevice.ViewModels;

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name ?? "");
        return true;
    }
}

public sealed class OnboardingViewModel(GatewayDeviceClient client) : ObservableViewModel
{
    public const int PairTimeoutSeconds = 20;

    private string _gatewayBaseUrl = "https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net";
    private string _enrollmentCode = "";
    private string _deviceName = "Front";
    private string? _status = "Issue a 6-digit code from the UAT portal, then tap Pair.";
    private string? _error;
    private bool _busy;

    public string GatewayBaseUrl
    {
        get => _gatewayBaseUrl;
        set => Set(ref _gatewayBaseUrl, value);
    }

    public string EnrollmentCode
    {
        get => _enrollmentCode;
        set => Set(ref _enrollmentCode, value);
    }

    public string DeviceName
    {
        get => _deviceName;
        set => Set(ref _deviceName, value);
    }

    public string? Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set
        {
            if (Set(ref _busy, value))
            {
                Raise(nameof(CanPair));
                Raise(nameof(PairButtonText));
            }
        }
    }

    public bool CanPair => !Busy;

    public string PairButtonText => Busy ? "Pairing…" : "Pair this tablet";

    public async Task<bool> PairAsync(CancellationToken ct = default)
    {
        var code = EnrollmentCode.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            Status = "Pairing did not start.";
            Error = "Enter the 6-digit code from the portal. It expires in 15 minutes.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(GatewayBaseUrl))
        {
            Status = "Pairing did not start.";
            Error = "Gateway URL is empty.";
            return false;
        }

        Busy = true;
        Error = null;
        Status = $"Contacting {GatewayBaseUrl.Trim()} …";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(PairTimeoutSeconds));
        try
        {
            var session = await client.EnrollAsync(GatewayBaseUrl, code, DeviceName, timeout.Token);
            Status = $"Paired with {session.StoreName} as “{session.DeviceName}”.";
            Error = null;
            return true;
        }
        catch (Exception ex)
        {
            Status = "Pairing failed.";
            Error = ex.Message;
            return false;
        }
        finally
        {
            Busy = false;
        }
    }
}

public sealed class BoardViewModel(GatewayDeviceClient client, IDeviceSessionStore sessions) : ObservableViewModel
{
    private DeviceSession? _session;
    private IReadOnlyList<DeviceOrderDto> _orders = [];
    private IReadOnlyList<TicketViewModel> _newTickets = [];
    private IReadOnlyList<TicketViewModel> _prepTickets = [];
    private IReadOnlyList<TicketViewModel> _readyTickets = [];
    private string? _error;
    private string? _snackbar;
    private string _clock = "";
    private string _connection = "Live";
    private bool _busy;
    private bool _offline;

    public DeviceSession? Session
    {
        get => _session;
        private set
        {
            if (Set(ref _session, value))
            {
                Raise(nameof(StoreName));
                Raise(nameof(DeviceName));
            }
        }
    }

    public IReadOnlyList<DeviceOrderDto> Orders
    {
        get => _orders;
        private set => Set(ref _orders, value);
    }

    public IReadOnlyList<TicketViewModel> NewTickets
    {
        get => _newTickets;
        private set
        {
            if (Set(ref _newTickets, value))
            {
                Raise(nameof(NewCount));
                Raise(nameof(BannerText));
                Raise(nameof(HasBanner));
            }
        }
    }

    public IReadOnlyList<TicketViewModel> PrepTickets
    {
        get => _prepTickets;
        private set
        {
            if (Set(ref _prepTickets, value))
            {
                Raise(nameof(PrepCount));
            }
        }
    }

    public IReadOnlyList<TicketViewModel> ReadyTickets
    {
        get => _readyTickets;
        private set
        {
            if (Set(ref _readyTickets, value))
            {
                Raise(nameof(ReadyCount));
                Raise(nameof(PaceLine));
            }
        }
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public string? Snackbar
    {
        get => _snackbar;
        set
        {
            if (Set(ref _snackbar, value))
            {
                Raise(nameof(HasSnackbar));
            }
        }
    }

    public bool HasSnackbar => !string.IsNullOrWhiteSpace(Snackbar);

    public string Clock
    {
        get => _clock;
        private set => Set(ref _clock, value);
    }

    public string Connection
    {
        get => _connection;
        private set => Set(ref _connection, value);
    }

    public bool Offline
    {
        get => _offline;
        private set
        {
            if (Set(ref _offline, value))
            {
                Raise(nameof(ConnectionColor));
            }
        }
    }

    public string ConnectionColor => Offline ? "#FFB454" : "#4ADE80";

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public string StoreName => Session?.StoreName ?? "MOOG";

    public string DeviceName => Session?.DeviceName ?? "";

    public DeviceFunction Functions => Session?.Functions ?? DeviceFunction.None;

    public bool Can(DeviceFunction flag) => Functions.HasFlag(flag);

    public event Func<TicketViewModel, Task>? OpenDetails;

    public int NewCount => NewTickets.Count;

    public int PrepCount => PrepTickets.Count;

    public int ReadyCount => ReadyTickets.Count;

    public bool HasBanner => NewCount > 0;

    public string BannerText => NewCount == 1 ? "1 NEW ORDER" : $"{NewCount} NEW ORDERS";

    public string PaceLine =>
        $"Waiting to be collected {ReadyCount}    Preparing {PrepCount}    New {NewCount}";

    public void Tick(DateTimeOffset utcNow)
    {
        Clock = utcNow.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var ticket in NewTickets.Concat(PrepTickets).Concat(ReadyTickets))
        {
            ticket.RefreshTimer(utcNow);
        }
    }

    public async Task LoadAsync(CancellationToken ct = default, bool showBusy = true)
    {
        if (showBusy)
        {
            Busy = true;
        }

        Error = null;
        try
        {
            Session = await sessions.GetAsync(ct);
            if (Session is null)
            {
                Orders = [];
                RebuildColumns();
                return;
            }

            await client.HeartbeatAsync(ct);
            Orders = await client.ListOrdersAsync(ct);
            Offline = false;
            Connection = "Live";
            RebuildColumns();
        }
        catch (Exception ex)
        {
            Offline = true;
            Connection = "Reconnecting…";
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task UnpairAsync(CancellationToken ct = default)
    {
        await sessions.ClearAsync(ct);
        Session = null;
        Orders = [];
        RebuildColumns();
    }

    public async Task RunPrimaryAsync(TicketViewModel ticket, CancellationToken ct = default)
    {
        ticket.Busy = true;
        Error = null;
        try
        {
            switch (ticket.Order.Status)
            {
                case OrderStatus.Preparing:
                    await client.MarkReadyAsync(ticket.Order.OrderRef, ct);
                    Snackbar = $"#{ticket.DisplayNumber} ready";
                    break;
                case OrderStatus.Ready:
                    await client.MarkCompletedAsync(ticket.Order.OrderRef, ct);
                    Snackbar = $"#{ticket.DisplayNumber} handed over";
                    break;
                default:
                    await client.MarkPreparingAsync(ticket.Order.OrderRef, ct);
                    Snackbar = $"#{ticket.DisplayNumber} accepted";
                    break;
            }

            await LoadAsync(ct, showBusy: false);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            ticket.Busy = false;
        }
    }

    public Task MarkPreparingAsync(string orderRef, CancellationToken ct = default) =>
        client.MarkPreparingAsync(orderRef, ct);

    public Task MarkReadyAsync(string orderRef, CancellationToken ct = default) =>
        client.MarkReadyAsync(orderRef, ct);

    public Task MarkCompletedAsync(string orderRef, CancellationToken ct = default) =>
        client.MarkCompletedAsync(orderRef, ct);

    public Task CancelAsync(string orderRef, CancelReason reason, CancellationToken ct = default) =>
        client.CancelAsync(orderRef, reason, ct);

    public Task DelayAsync(string orderRef, int minutes, CancellationToken ct = default) =>
        client.DelayAsync(orderRef, minutes, ct);

    private void RebuildColumns()
    {
        var tickets = Orders
            .Where(o => o.Status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.Ready)
            .Select(o => new TicketViewModel(o, Functions, t => RunPrimaryAsync(t), OpenAsync))
            .ToList();

        NewTickets = tickets
            .Where(t => t.ColumnKey == "new")
            .OrderBy(t => t.Order.PlacedAtUtc)
            .ToList();
        PrepTickets = tickets
            .Where(t => t.ColumnKey == "prep")
            .OrderBy(t => t.Order.ScheduledForUtc ?? t.Order.PlacedAtUtc)
            .ToList();
        ReadyTickets = tickets
            .Where(t => t.ColumnKey == "ready")
            .OrderBy(t => t.Order.ScheduledForUtc ?? t.Order.PlacedAtUtc)
            .ToList();
    }

    private Task OpenAsync(TicketViewModel ticket) =>
        OpenDetails?.Invoke(ticket) ?? Task.CompletedTask;
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Gateway.Domain.Devices;
using Gateway.Domain.Enums;
using StoreDevice.Client;

namespace StoreDevice.ViewModels;

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class OnboardingViewModel(GatewayDeviceClient client) : ObservableViewModel
{
    private string _gatewayBaseUrl = "http://10.0.2.2:5175";
    private string _enrollmentCode = "";
    private string _deviceName = "Pass kitchen";
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

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public async Task PairAsync(CancellationToken ct = default)
    {
        Busy = true;
        Error = null;
        try
        {
            await client.EnrollAsync(GatewayBaseUrl, EnrollmentCode, DeviceName, ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            throw;
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
    private string? _error;
    private bool _busy;

    public DeviceSession? Session
    {
        get => _session;
        private set => Set(ref _session, value);
    }

    public IReadOnlyList<DeviceOrderDto> Orders
    {
        get => _orders;
        private set => Set(ref _orders, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public string StoreName => Session?.StoreName ?? "";

    public DeviceFunction Functions => Session?.Functions ?? DeviceFunction.None;

    public bool Can(DeviceFunction flag) => Functions.HasFlag(flag);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        Error = null;
        try
        {
            Session = await sessions.GetAsync(ct);
            if (Session is null)
            {
                Orders = [];
                return;
            }

            await client.HeartbeatAsync(ct);
            Orders = await client.ListOrdersAsync(ct);
        }
        catch (Exception ex)
        {
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
}

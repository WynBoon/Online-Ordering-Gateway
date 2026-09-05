using Gateway.Domain.Devices;
using Gateway.Domain.Enums;

namespace StoreDevice.Client;

public sealed class DeviceSession
{
    public required string GatewayBaseUrl { get; set; }
    public required string DeviceToken { get; set; }
    public Guid DeviceId { get; set; }
    public Guid StoreId { get; set; }
    public required string StoreName { get; set; }
    public required string DeviceName { get; set; }
    public DeviceFunction Functions { get; set; }
}

public interface IDeviceSessionStore
{
    Task<DeviceSession?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(DeviceSession session, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public sealed record ClaimedEnrollmentDto(
    Guid DeviceId,
    Guid StoreId,
    string StoreName,
    string DeviceName,
    string DeviceToken,
    DeviceFunction Functions);

public sealed record DeviceSessionDto(
    Guid DeviceId,
    Guid StoreId,
    string StoreName,
    string DeviceName,
    DeviceStatus Status,
    DeviceFunction Functions,
    bool Online,
    DateTimeOffset? LastHeartbeatAtUtc);

public sealed record DeviceOrderItemDto(string Name, int Quantity, IReadOnlyList<string> Modifiers, string? Notes);

public sealed record DeviceOrderDto(
    string OrderRef,
    string DisplayId,
    string SourceChannel,
    string Fulfillment,
    OrderStatus Status,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? ScheduledForUtc,
    long TotalCents,
    string Currency,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? Notes,
    string? PosOrderId,
    IReadOnlyList<DeviceOrderItemDto> Items);

internal sealed record ErrorBody(string? Error);

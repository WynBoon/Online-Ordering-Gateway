using Gateway.Domain.Devices;
using Gateway.Domain.Enums;

namespace Gateway.Application.Devices;

public sealed record IssuedEnrollment(Guid DeviceId, string EnrollmentCode, DateTimeOffset ExpiresAtUtc);

public sealed record ClaimedEnrollment(
    Guid DeviceId, Guid StoreId, string StoreName, string DeviceName,
    string DeviceToken, DeviceFunction Functions);

public sealed record DeviceOrderItemDto(string Name, int Quantity, IReadOnlyList<string> Modifiers, string? Notes);

public sealed record DeviceOrderDto(
    string OrderRef, string DisplayId, string SourceChannel, string Fulfillment,
    OrderStatus Status, DateTimeOffset PlacedAtUtc, DateTimeOffset? ScheduledForUtc,
    long TotalCents, string Currency,
    string? CustomerName, string? CustomerPhone, string? CustomerEmail,
    string? Notes, string? PosOrderId,
    IReadOnlyList<DeviceOrderItemDto> Items);

public sealed record DeviceSessionDto(
    Guid DeviceId, Guid StoreId, string StoreName, string DeviceName,
    DeviceStatus Status, DeviceFunction Functions, bool Online,
    DateTimeOffset? LastHeartbeatAtUtc);

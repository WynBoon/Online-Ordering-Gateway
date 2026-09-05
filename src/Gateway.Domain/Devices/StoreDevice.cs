namespace Gateway.Domain.Devices;

public sealed class StoreDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public required string Name { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.PendingEnrollment;
    public DeviceFunction Functions { get; set; }
    public string? EnrollmentCodeHash { get; set; }
    public DateTimeOffset? EnrollmentExpiresAtUtc { get; set; }
    public string? TokenHash { get; set; }
    public string? HardwareFingerprint { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EnrolledAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public bool IsOnline(DateTimeOffset utcNow, TimeSpan? leash = null) =>
        Status == DeviceStatus.Active
        && LastHeartbeatAtUtc is { } beat
        && utcNow - beat <= (leash ?? TimeSpan.FromSeconds(45));
}

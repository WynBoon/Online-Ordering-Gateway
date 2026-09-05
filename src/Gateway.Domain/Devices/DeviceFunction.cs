namespace Gateway.Domain.Devices;

[Flags]
public enum DeviceFunction
{
    None = 0,
    MarkPreparing = 1 << 0,       // → OrderStatus.Preparing
    MarkReady = 1 << 1,           // → OrderStatus.Ready
    MarkCompleted = 1 << 2,       // → OrderStatus.Completed
    CancelOrder = 1 << 3,         // → OrderStatus.Cancelled
    AdjustPromiseTime = 1 << 4,   // bump CanonicalOrder.ScheduledForUtc; no extra OH status
    ViewCustomerContact = 1 << 5, // show phone/email
    ViewOrderNotes = 1 << 6       // show order + item notes
}

public static class DeviceFunctionSets
{
    public static DeviceFunction KitchenStatusOfRecord =>
        DeviceFunction.MarkPreparing
        | DeviceFunction.MarkReady
        | DeviceFunction.MarkCompleted
        | DeviceFunction.CancelOrder
        | DeviceFunction.AdjustPromiseTime
        | DeviceFunction.ViewCustomerContact
        | DeviceFunction.ViewOrderNotes; // numeric value 127

    public static DeviceFunction KitchenBackup =>
        DeviceFunction.MarkPreparing
        | DeviceFunction.MarkReady
        | DeviceFunction.MarkCompleted
        | DeviceFunction.ViewCustomerContact
        | DeviceFunction.ViewOrderNotes;
        // no CancelOrder, no AdjustPromiseTime by default

    public static DeviceFunction RecommendedFor(Enums.PosType posType) =>
        posType == Enums.PosType.Gaap ? KitchenStatusOfRecord : KitchenBackup;
}

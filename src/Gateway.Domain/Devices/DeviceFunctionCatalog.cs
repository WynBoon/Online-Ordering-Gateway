namespace Gateway.Domain.Devices;

public sealed record DeviceFunctionDescriptor(
    DeviceFunction Flag,
    string Code,
    string Title,
    string Description);

public static class DeviceFunctionCatalog
{
    public static IReadOnlyList<DeviceFunctionDescriptor> All { get; } =
    [
        new(DeviceFunction.MarkPreparing, "mark_preparing", "Mark preparing",
            "Kitchen has started the ticket. Relayed to Order Harmony as preparing."),
        new(DeviceFunction.MarkReady, "mark_ready", "Mark ready",
            "Food is ready for collection or the driver. Relayed as ready."),
        new(DeviceFunction.MarkCompleted, "mark_completed", "Mark completed",
            "Handed to the customer or driver. Relayed as completed."),
        new(DeviceFunction.CancelOrder, "cancel_order", "Cancel order",
            "Store cannot fulfil. Relayed as cancelled with a reason."),
        new(DeviceFunction.AdjustPromiseTime, "adjust_promise_time", "Adjust promise time",
            "Add minutes when the kitchen is running late."),
        new(DeviceFunction.ViewCustomerContact, "view_customer_contact", "View customer contact",
            "Show the customer phone and email on the device."),
        new(DeviceFunction.ViewOrderNotes, "view_order_notes", "View order notes",
            "Show channel and item special instructions.")
    ];
}

namespace Gateway.Domain.Enums;

/// <summary>
/// Which ordering channel a store's inbound <c>ChannelConnection</c> is wired to.
/// Only Order Harmony today; modelled as an enum (not hardcoded) so a second
/// channel is a value addition, not a schema change.
/// </summary>
public enum ChannelType
{
    OrderHarmony
}

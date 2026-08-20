namespace Gateway.Domain.Menu;

/// <summary>
/// Enforced by Order Harmony before injection, so <see cref="MinSelect"/>/
/// <see cref="MaxSelect"/> must be accurate. Whether GAAP can even expose these
/// rules is an open question — ARCHITECTURE.md §10, GAAP item 5.
/// </summary>
public sealed class ModifierGroup
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public List<MenuModifier> Modifiers { get; set; } = [];
}

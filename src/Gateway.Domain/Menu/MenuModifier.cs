namespace Gateway.Domain.Menu;

/// <summary><see cref="ExternalId"/> is the POS's own native modifier id — see CanonicalModifier.</summary>
public sealed class MenuModifier
{
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public long PriceDeltaCents { get; set; }
}

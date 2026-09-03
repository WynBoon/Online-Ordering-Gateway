using Gateway.Adapters.Pilot.Dtos;
using Gateway.Application.Ports;
using Gateway.Domain.Menu;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Reshapes Pilot's <c>GET /SalesProducts/Menu</c> PluItems into the category tree
/// Order Harmony expects. PLU values pass through unchanged (ARCHITECTURE.md §7).
/// Option groups are published without min/max — Pilot's spec does not expose those
/// rules (ARCHITECTURE.md §10).
/// </summary>
public sealed class PilotMenuAdapter(PilotApiClient client) : IPosMenuAdapter
{
    public async Task<CanonicalMenu> GetMenuAsync(PosConnection connection, CancellationToken ct)
    {
        var response = await client.GetMenuAsync(connection, ct);
        if (response.Status == false)
        {
            throw new HttpRequestException(response.Message ?? "Pilot menu returned status=false.");
        }

        var categories = (response.PluItems ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Plu))
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Dtab) ? "Uncategorised" : p.Dtab)
            .Select(g => new MenuCategory
            {
                ExternalId = g.Key,
                Name = g.Key,
                Products = g.Select(MapProduct).ToList()
            })
            .ToList();

        return new CanonicalMenu { StoreId = connection.StoreId, Categories = categories };
    }

    private static MenuProduct MapProduct(PilotPluItem item) => new()
    {
        ExternalId = item.Plu!,
        Name = string.IsNullOrWhiteSpace(item.ItemName) ? item.Plu! : item.ItemName,
        PriceCents = ToCents(item.Price),
        ModifierGroups = (item.Options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionName) && o.OptionItems is { Count: > 0 })
            .Select(o => new ModifierGroup
            {
                ExternalId = o.OptionName!,
                Name = o.OptionName!,
                MinSelect = 0,
                MaxSelect = o.OptionItems!.Count,
                Modifiers = o.OptionItems
                    .Where(i => !string.IsNullOrWhiteSpace(i.Plu))
                    .Select(i => new MenuModifier
                    {
                        ExternalId = i.Plu!,
                        Name = string.IsNullOrWhiteSpace(i.ItemName) ? i.Plu! : i.ItemName,
                        PriceDeltaCents = ToCents(i.Price)
                    })
                    .ToList()
            })
            .ToList()
    };

    internal static long ToCents(double majorUnits) => (long)Math.Round(majorUnits * 100, MidpointRounding.AwayFromZero);
}

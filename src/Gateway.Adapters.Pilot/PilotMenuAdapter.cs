using Gateway.Application.Ports;
using Gateway.Domain.Menu;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Pilot;

/// <summary>
/// Reshapes Pilot's flat product list into the category tree Order Harmony
/// expects. PLU values pass through unchanged (ARCHITECTURE.md §7). Modifier
/// group min/max rules aren't in the confirmed schema — open question,
/// ARCHITECTURE.md §10 — so modifier groups are omitted here pending that answer.
/// </summary>
public sealed class PilotMenuAdapter(PilotApiClient client) : IPosMenuAdapter
{
    public async Task<CanonicalMenu> GetMenuAsync(PosConnection connection, CancellationToken ct)
    {
        var products = await client.GetMenuAsync(connection, ct);

        var categories = products
            .GroupBy(p => p.Category ?? "Uncategorised")
            .Select(g => new MenuCategory
            {
                ExternalId = g.Key,
                Name = g.Key,
                Products = g.Select(p => new MenuProduct
                {
                    ExternalId = p.Plu,
                    Name = p.Description,
                    PriceCents = p.Price
                }).ToList()
            })
            .ToList();

        return new CanonicalMenu { StoreId = Guid.Empty, Categories = categories };
    }
}

using Gateway.Application.Ports;
using Gateway.Domain.Menu;
using Gateway.Domain.Tenancy;

namespace Gateway.Adapters.Gaap;

/// <summary>
/// Reshapes GAAP's flat /products + /groups lists into the category tree Order
/// Harmony expects. Product ids pass through unchanged (ARCHITECTURE.md §7) — no
/// stored mapping. Modifier groups are omitted entirely: GAAP's product catalogue
/// doesn't expose an add-on/modifier concept, even though order submission has an
/// addOns field (open question, §10) — until that's resolved, a GAAP-backed
/// store's published menu has no modifiers.
/// </summary>
public sealed class GaapMenuAdapter(GaapApiClient client) : IPosMenuAdapter
{
    private const int PageSize = 100;

    public async Task<CanonicalMenu> GetMenuAsync(PosConnection connection, CancellationToken ct)
    {
        var groups = await FetchAllGroupsAsync(connection, ct);
        var products = await FetchAllProductsAsync(connection, ct);

        var categories = groups
            .Select(g => new MenuCategory
            {
                ExternalId = g.Id,
                Name = g.Name,
                // GAAP's product-to-group linkage isn't in the confirmed schema fields —
                // TODO once sandbox data is available: filter products by their group.
                Products = []
            })
            .ToList();

        var uncategorised = new MenuCategory { ExternalId = "uncategorised", Name = "Uncategorised" };
        foreach (var product in products.Where(p => p.Active))
        {
            uncategorised.Products.Add(new MenuProduct
            {
                ExternalId = product.Id,
                Name = product.Name,
                Description = product.KitchenDescription,
                PriceCents = 0 // TODO: extract from GaapProduct.Pricing once its shape is confirmed against real data.
            });
        }

        if (uncategorised.Products.Count > 0)
        {
            categories.Add(uncategorised);
        }

        return new CanonicalMenu { StoreId = Guid.Empty, Categories = categories };
    }

    private async Task<List<Dtos.GaapProduct>> FetchAllProductsAsync(PosConnection connection, CancellationToken ct)
    {
        var all = new List<Dtos.GaapProduct>();
        int skip = 0;
        while (true)
        {
            var page = await client.GetProductsAsync(connection, PageSize, skip, ct);
            all.AddRange(page.Data);
            if (page.Data.Count < PageSize)
            {
                break;
            }

            skip += PageSize;
        }

        return all;
    }

    private async Task<List<Dtos.GaapProductGroup>> FetchAllGroupsAsync(PosConnection connection, CancellationToken ct)
    {
        var all = new List<Dtos.GaapProductGroup>();
        int skip = 0;
        while (true)
        {
            var page = await client.GetGroupsAsync(connection, PageSize, skip, ct);
            all.AddRange(page.Data);
            if (page.Data.Count < PageSize)
            {
                break;
            }

            skip += PageSize;
        }

        return all;
    }
}

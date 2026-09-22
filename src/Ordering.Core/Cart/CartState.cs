namespace Ordering.Cart;

public sealed class CartState
{
    public const string Pickup = "pickup";
    public const string Delivery = "delivery";
    public const string DineIn = "dine_in";

    public string FulfillmentType { get; set; } = Pickup;
    public string CustomerName { get; set; } = "Test customer";
    public string CustomerPhone { get; set; } = "0820000000";
    public string CustomerEmail { get; set; } = "";
    public string DeliveryLine1 { get; set; } = "";
    public string DeliveryCity { get; set; } = "";
    public string DeliveryPostalCode { get; set; } = "";
    public string Notes { get; set; } = "";
    public long DeliveryFeeCents { get; set; }
    public long TipCents { get; set; }
    public string Currency { get; set; } = "ZAR";

    public List<CartLine> Lines { get; } = [];

    public long SubtotalCents => Lines.Sum(l => l.TotalPriceCents);

    public long TaxCents => 0;

    public long TotalCents => SubtotalCents + TaxCents + DeliveryFeeCents + TipCents;

    public int ItemCount => Lines.Sum(l => l.Quantity);

    public void ClearLines() => Lines.Clear();

    public string? ValidateCheckout()
    {
        if (Lines.Count == 0)
        {
            return "Add something to the cart first.";
        }

        if (FulfillmentType == Delivery && string.IsNullOrWhiteSpace(DeliveryLine1))
        {
            return "Delivery needs a street address.";
        }

        return null;
    }

    public static string FormatZar(long cents)
    {
        var value = cents / 100m;
        return $"R {value:0.00}";
    }
}

public sealed class CartLine
{
    public required string ExternalProductId { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; } = 1;
    public long UnitPriceCents { get; set; }
    public string? Notes { get; set; }
    public List<CartModifier> Modifiers { get; set; } = [];

    public long TotalPriceCents =>
        (UnitPriceCents + Modifiers.Sum(m => m.PriceDeltaCents * m.Quantity)) * Quantity;
}

public sealed class CartModifier
{
    public required string ExternalModifierId { get; set; }
    public string? GroupExternalId { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; } = 1;
    public long PriceDeltaCents { get; set; }
}

public static class ModifierRules
{
    public static string? Validate(IReadOnlyList<(int MinSelect, int MaxSelect, string Name, int SelectedCount)> groups)
    {
        foreach (var group in groups)
        {
            if (group.SelectedCount < group.MinSelect)
            {
                return $"Choose at least {group.MinSelect} from {group.Name}.";
            }

            if (group.MaxSelect > 0 && group.SelectedCount > group.MaxSelect)
            {
                return $"Choose at most {group.MaxSelect} from {group.Name}.";
            }
        }

        return null;
    }
}

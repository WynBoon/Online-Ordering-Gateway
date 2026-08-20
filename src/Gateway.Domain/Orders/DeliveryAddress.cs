namespace Gateway.Domain.Orders;

/// <summary>Required only when <see cref="CanonicalOrder.FulfillmentType"/> is Delivery.</summary>
public sealed class DeliveryAddress
{
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }
}

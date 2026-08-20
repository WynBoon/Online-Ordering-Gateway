namespace Gateway.Domain.Orders;

public sealed class CustomerInfo
{
    public string? Name { get; set; }

    /// <summary>May be a masked relay number — that's fine, pass it through as given.</summary>
    public string? Phone { get; set; }

    public string? Email { get; set; }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ordering.Cart;
using Ordering.Client;

namespace Ordering.ViewModels;

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MenuSelection
{
    public MenuProductDto? Product { get; set; }
}

public sealed class TrackingSelection
{
    public string? OrderRef { get; set; }
    public ChannelOrderDto? Placed { get; set; }
}

public sealed class LabContext
{
    public string? LastIdempotencyKey { get; set; }
    public PlaceOrderCommand? LastCommand { get; set; }
}

public sealed class ConnectViewModel(IOrderingClient client, IOrderingSessionStore sessions) : ObservableViewModel
{
    private string _gatewayBaseUrl = GatewayEndpoints.UatApi;
    private string _locationKey = "";
    private string? _error;
    private string? _health;
    private bool _busy;

    public string GatewayBaseUrl
    {
        get => _gatewayBaseUrl;
        set => Set(ref _gatewayBaseUrl, value);
    }

    public string LocationKey
    {
        get => _locationKey;
        set => Set(ref _locationKey, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public string? Health
    {
        get => _health;
        set => Set(ref _health, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(LocationKey))
        {
            Error = "Paste the UAT store location key from the portal.";
            return;
        }

        Busy = true;
        Error = null;
        Health = null;
        try
        {
            await sessions.SaveAsync(new OrderingSession
            {
                GatewayBaseUrl = GatewayBaseUrl.Trim(),
                LocationKey = LocationKey.Trim(),
                SourceChannel = "test",
                LocationId = "uat"
            }, ct);

            var health = await client.GetHealthAsync(ct);
            Health = health.Ok ? "Till path is healthy." : $"Degraded: {health.Detail ?? "POS ping failed"}";
        }
        catch (Exception ex)
        {
            await sessions.ClearAsync(ct);
            Error = ex.Message;
            throw;
        }
        finally
        {
            Busy = false;
        }
    }
}

public sealed record MenuProductRow(string Category, MenuProductDto Product, string PriceLabel);

public sealed class MenuViewModel(IOrderingClient client, IOrderingSessionStore sessions) : ObservableViewModel
{
    private IReadOnlyList<MenuProductRow> _allProducts = [];
    private IReadOnlyList<string> _categories = [];
    private IReadOnlyList<MenuProductRow> _products = [];
    private string? _selectedCategory;
    private string? _error;
    private bool _busy;

    public IReadOnlyList<string> Categories
    {
        get => _categories;
        private set => Set(ref _categories, value);
    }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        private set => Set(ref _selectedCategory, value);
    }

    public IReadOnlyList<MenuProductRow> Products
    {
        get => _products;
        private set => Set(ref _products, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default) =>
        await sessions.GetAsync(ct) is not null;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        Error = null;
        try
        {
            var menu = await client.GetMenuAsync(ct);
            _allProducts = menu.Categories
                .SelectMany(c => c.Products.Select(p => new MenuProductRow(
                    c.Name,
                    p,
                    CartState.FormatZar(p.PriceCents))))
                .ToList();
            Categories = menu.Categories
                .Select(c => c.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var keep = SelectedCategory is not null
                       && Categories.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase)
                ? SelectedCategory
                : Categories.FirstOrDefault();
            SelectCategory(keep);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _allProducts = [];
            Categories = [];
            Products = [];
            SelectedCategory = null;
        }
        finally
        {
            Busy = false;
        }
    }

    public void SelectCategory(string? category)
    {
        SelectedCategory = category;
        Products = string.IsNullOrWhiteSpace(category)
            ? []
            : _allProducts.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task DisconnectAsync(CancellationToken ct = default) =>
        await sessions.ClearAsync(ct);
}

public sealed class ItemViewModel : ObservableViewModel
{
    private MenuProductDto? _product;
    private int _quantity = 1;
    private string _notes = "";
    private string? _error;

    public MenuProductDto? Product
    {
        get => _product;
        private set
        {
            Set(ref _product, value);
            Raise(nameof(Title));
            Raise(nameof(PriceLabel));
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            Set(ref _quantity, Math.Max(1, value));
            Raise(nameof(PriceLabel));
        }
    }

    public string Notes
    {
        get => _notes;
        set => Set(ref _notes, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public string Title => Product?.Name ?? "Item";

    public string PriceLabel => Product is null ? "" : CartState.FormatZar(LineTotalCents());

    public ObservableCollection<ModifierGroupChoice> Groups { get; } = [];

    public void Load(MenuProductDto product)
    {
        Product = product;
        Quantity = 1;
        Notes = "";
        Error = null;
        Groups.Clear();
        foreach (var group in product.ModifierGroups)
        {
            Groups.Add(new ModifierGroupChoice(group, OnGroupChanged));
        }

        Raise(nameof(PriceLabel));
    }

    public string? TryAddToCart(CartState cart)
    {
        if (Product is null)
        {
            return "No item selected.";
        }

        var ruleError = ModifierRules.Validate(Groups.Select(g => (g.Group.MinSelect, g.Group.MaxSelect, g.Group.Name, g.SelectedCount)).ToList());
        if (ruleError is not null)
        {
            Error = ruleError;
            return ruleError;
        }

        cart.Lines.Add(new CartLine
        {
            ExternalProductId = Product.ExternalId,
            Name = Product.Name,
            Quantity = Quantity,
            UnitPriceCents = Product.PriceCents,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            Modifiers = Groups.SelectMany(g => g.SelectedModifiers).ToList()
        });
        return null;
    }

    private void OnGroupChanged() => Raise(nameof(PriceLabel));

    private long LineTotalCents()
    {
        if (Product is null)
        {
            return 0;
        }

        var deltas = Groups.SelectMany(g => g.SelectedModifiers).Sum(m => m.PriceDeltaCents * m.Quantity);
        return (Product.PriceCents + deltas) * Quantity;
    }
}

public sealed class ModifierGroupChoice : ObservableViewModel
{
    private readonly Action _changed;

    public ModifierGroupChoice(ModifierGroupDto group, Action changed)
    {
        Group = group;
        _changed = changed;
        Options = group.Modifiers.Select(m => new ModifierOption(m, OnToggled)).ToList();
    }

    public ModifierGroupDto Group { get; }

    public IReadOnlyList<ModifierOption> Options { get; }

    public string Caption =>
        Group.MinSelect > 0
            ? $"{Group.Name} (pick {Group.MinSelect}–{Group.MaxSelect})"
            : $"{Group.Name} (optional, max {Group.MaxSelect})";

    public int SelectedCount => Options.Count(o => o.IsSelected);

    public IEnumerable<CartModifier> SelectedModifiers =>
        Options.Where(o => o.IsSelected).Select(o => new CartModifier
        {
            ExternalModifierId = o.Modifier.ExternalId,
            GroupExternalId = Group.ExternalId,
            Name = o.Modifier.Name,
            Quantity = 1,
            PriceDeltaCents = o.Modifier.PriceDeltaCents
        });

    private void OnToggled(ModifierOption option)
    {
        if (option.IsSelected && Group.MaxSelect == 1)
        {
            foreach (var other in Options.Where(o => o != option))
            {
                other.SetSelectedSilent(false);
            }
        }

        if (option.IsSelected && Group.MaxSelect > 1 && SelectedCount > Group.MaxSelect)
        {
            option.SetSelectedSilent(false);
        }

        _changed();
    }
}

public sealed class ModifierOption : ObservableViewModel
{
    private readonly Action<ModifierOption> _toggled;
    private bool _isSelected;

    public ModifierOption(MenuModifierDto modifier, Action<ModifierOption> toggled)
    {
        Modifier = modifier;
        _toggled = toggled;
    }

    public MenuModifierDto Modifier { get; }

    public string Label =>
        Modifier.PriceDeltaCents == 0
            ? Modifier.Name
            : $"{Modifier.Name} ({CartState.FormatZar(Modifier.PriceDeltaCents)})";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            Raise(nameof(IsSelected));
            _toggled(this);
        }
    }

    internal void SetSelectedSilent(bool value)
    {
        if (_isSelected == value)
        {
            return;
        }

        _isSelected = value;
        Raise(nameof(IsSelected));
    }
}

public sealed class CartViewModel(IOrderingClient client, LabContext lab) : ObservableViewModel
{
    private string? _error;
    private bool _busy;
    private InjectionResult? _lastResult;

    public CartState Cart { get; } = new();

    public IReadOnlyList<string> FulfillmentOptions { get; } = [CartState.Pickup, CartState.Delivery, CartState.DineIn];

    public string FulfillmentType
    {
        get => Cart.FulfillmentType;
        set
        {
            Cart.FulfillmentType = value;
            Raise(nameof(FulfillmentType));
            Raise(nameof(IsDelivery));
        }
    }

    public bool IsDelivery => Cart.FulfillmentType == CartState.Delivery;

    public string CustomerName
    {
        get => Cart.CustomerName;
        set => Cart.CustomerName = value;
    }

    public string CustomerPhone
    {
        get => Cart.CustomerPhone;
        set => Cart.CustomerPhone = value;
    }

    public string DeliveryLine1
    {
        get => Cart.DeliveryLine1;
        set => Cart.DeliveryLine1 = value;
    }

    public string Notes
    {
        get => Cart.Notes;
        set => Cart.Notes = value;
    }

    public string SubtotalLabel => CartState.FormatZar(Cart.SubtotalCents);

    public string TotalLabel => CartState.FormatZar(Cart.TotalCents);

    public string ItemCountLabel => Cart.ItemCount == 1 ? "1 item" : $"{Cart.ItemCount} items";

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public InjectionResult? LastResult
    {
        get => _lastResult;
        private set => Set(ref _lastResult, value);
    }

    public void RefreshTotals()
    {
        Raise(nameof(SubtotalLabel));
        Raise(nameof(TotalLabel));
        Raise(nameof(ItemCountLabel));
        Raise(nameof(IsDelivery));
    }

    public void RemoveLine(CartLine line)
    {
        Cart.Lines.Remove(line);
        RefreshTotals();
    }

    public async Task<InjectionResult?> PlaceAsync(CancellationToken ct = default)
    {
        Error = null;
        var validation = Cart.ValidateCheckout();
        if (validation is not null)
        {
            Error = validation;
            return null;
        }

        Busy = true;
        try
        {
            var command = ToCommand(Guid.NewGuid().ToString("N"), NextDisplayId());
            var result = await client.PlaceOrderAsync(command, ct);
            LastResult = result;
            lab.LastCommand = command;
            lab.LastIdempotencyKey = result.IdempotencyKey;
            if (!result.Success)
            {
                Error = result.ErrorMessage ?? "The gateway rejected the order.";
                return result;
            }

            Cart.ClearLines();
            RefreshTotals();
            return result;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return null;
        }
        finally
        {
            Busy = false;
        }
    }

    public PlaceOrderCommand ToCommand(string orderRef, string displayId, string? idempotencyKey = null, string? unknownPlu = null) =>
        new()
        {
            OrderRef = orderRef,
            DisplayId = displayId,
            FulfillmentType = Cart.FulfillmentType,
            CustomerName = Cart.CustomerName,
            CustomerPhone = Cart.CustomerPhone,
            CustomerEmail = Cart.CustomerEmail,
            DeliveryLine1 = Cart.DeliveryLine1,
            DeliveryCity = Cart.DeliveryCity,
            DeliveryPostalCode = Cart.DeliveryPostalCode,
            Items = Cart.Lines.ToList(),
            SubtotalCents = Cart.SubtotalCents,
            TaxCents = Cart.TaxCents,
            DeliveryFeeCents = Cart.DeliveryFeeCents,
            TipCents = Cart.TipCents,
            TotalCents = Cart.TotalCents,
            Currency = Cart.Currency,
            Notes = string.IsNullOrWhiteSpace(Cart.Notes) ? null : Cart.Notes,
            IdempotencyKey = idempotencyKey,
            UnknownPluOverride = unknownPlu
        };

    private static string NextDisplayId() =>
        "T" + DateTime.UtcNow.ToString("HHmmss");
}

public sealed class TrackingViewModel(IOrderingClient client) : ObservableViewModel
{
    private static readonly string[] Ladder = ["accepted", "preparing", "ready", "completed"];

    private ChannelOrderDto? _order;
    private string? _error;
    private bool _busy;

    public ChannelOrderDto? Order
    {
        get => _order;
        private set
        {
            Set(ref _order, value);
            Raise(nameof(Headline));
            Raise(nameof(StatusLabel));
            Raise(nameof(IsTerminal));
            Raise(nameof(LadderHint));
        }
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public string Headline => Order is null ? "Order" : $"#{Order.DisplayId}";

    public string StatusLabel => Order?.Status ?? "";

    public bool IsTerminal =>
        string.Equals(Order?.Status, "completed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Order?.Status, "cancelled", StringComparison.OrdinalIgnoreCase);

    public void Seed(ChannelOrderDto placed)
    {
        Order = placed;
        Error = null;
    }

    public string LadderHint
    {
        get
        {
            if (Order is null)
            {
                return "";
            }

            if (string.Equals(Order.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return Order.CancelReason is null ? "Cancelled." : $"Cancelled ({Order.CancelReason}).";
            }

            var current = Array.IndexOf(Ladder, Order.Status);
            if (current < 0)
            {
                return Order.Status;
            }

            return string.Join(" → ", Ladder.Select((s, i) => i <= current ? s.ToUpperInvariant() : s));
        }
    }

    public async Task LoadAsync(string orderRef, CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            Order = await client.GetOrderAsync(orderRef, ct);
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}

public sealed class OrdersViewModel(IOrderingClient client) : ObservableViewModel
{
    private IReadOnlyList<ChannelOrderDto> _orders = [];
    private string? _error;
    private bool _busy;

    public IReadOnlyList<ChannelOrderDto> Orders
    {
        get => _orders;
        private set => Set(ref _orders, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        Error = null;
        try
        {
            Orders = await client.ListOrdersAsync(ct);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Orders = [];
        }
        finally
        {
            Busy = false;
        }
    }
}

public sealed class LabViewModel(IOrderingClient client, IOrderingSessionStore sessions, CartViewModel cart, LabContext lab) : ObservableViewModel
{
    private string? _result;
    private bool _busy;

    public string? Result
    {
        get => _result;
        set => Set(ref _result, value);
    }

    public bool Busy
    {
        get => _busy;
        set => Set(ref _busy, value);
    }

    public async Task ReplayIdempotencyAsync(CancellationToken ct = default)
    {
        if (lab.LastCommand is null || string.IsNullOrWhiteSpace(lab.LastIdempotencyKey))
        {
            Result = "Place an order first, then replay its Idempotency-Key.";
            return;
        }

        await RunAsync(async () =>
        {
            var replay = Clone(lab.LastCommand, lab.LastIdempotencyKey);
            var result = await client.PlaceOrderAsync(replay, ct);
            Result = result.Success
                ? $"Replay {result.StatusCode}: same pos_order_id {result.PosOrderId} (no second ticket if the gateway cached the 201)."
                : $"Replay failed {result.StatusCode} {result.ErrorCode}: {result.ErrorMessage}";
        });
    }

    public async Task UnknownPluAsync(CancellationToken ct = default)
    {
        if (cart.Cart.Lines.Count == 0)
        {
            Result = "Add a real menu item to the cart first, then this will swap the first PLU for UNKNOWN-PLU.";
            return;
        }

        await RunAsync(async () =>
        {
            var command = cart.ToCommand("ORD-" + Guid.NewGuid().ToString("N"), "LAB", unknownPlu: "UNKNOWN-PLU");
            var result = await client.PlaceOrderAsync(command, ct);
            Result = result.Success
                ? "Unexpected 201 for an unknown PLU."
                : $"{result.StatusCode} {result.ErrorCode}: {result.ErrorMessage} (retryable={result.Retryable})";
        });
    }

    public async Task PausedStoreAsync(CancellationToken ct = default)
    {
        var session = await sessions.GetAsync(ct);
        if (session is null)
        {
            Result = "Connect first.";
            return;
        }

        if (cart.Cart.Lines.Count == 0)
        {
            Result = "Add something to the cart first.";
            return;
        }

        await RunAsync(async () =>
        {
            var previous = session.LocationKey;
            session.LocationKey = "dev-paused-location-key";
            await sessions.SaveAsync(session, ct);
            try
            {
                var command = cart.ToCommand("ORD-" + Guid.NewGuid().ToString("N"), "PAUSE");
                var result = await client.PlaceOrderAsync(command, ct);
                Result = result.Success
                    ? "Unexpected 201 on the paused store."
                    : $"{result.StatusCode} {result.ErrorCode}: {result.ErrorMessage} (retryable={result.Retryable})";
            }
            finally
            {
                session.LocationKey = previous;
                await sessions.SaveAsync(session, ct);
            }
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        Busy = true;
        Result = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Result = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    private static PlaceOrderCommand Clone(PlaceOrderCommand source, string idempotencyKey) =>
        new()
        {
            OrderRef = source.OrderRef,
            DisplayId = source.DisplayId,
            FulfillmentType = source.FulfillmentType,
            ScheduledFor = source.ScheduledFor,
            CustomerName = source.CustomerName,
            CustomerPhone = source.CustomerPhone,
            CustomerEmail = source.CustomerEmail,
            DeliveryLine1 = source.DeliveryLine1,
            DeliveryCity = source.DeliveryCity,
            DeliveryPostalCode = source.DeliveryPostalCode,
            Items = source.Items,
            SubtotalCents = source.SubtotalCents,
            TaxCents = source.TaxCents,
            DeliveryFeeCents = source.DeliveryFeeCents,
            TipCents = source.TipCents,
            TotalCents = source.TotalCents,
            Currency = source.Currency,
            Notes = source.Notes,
            IdempotencyKey = idempotencyKey
        };
}

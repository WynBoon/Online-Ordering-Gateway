using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class ItemPage : ContentPage
{
    private readonly ItemViewModel _vm;
    private readonly MenuSelection _selection;
    private readonly CartViewModel _cart;

    public ItemPage()
        : this(
            PageServices.Resolve<ItemViewModel>(),
            PageServices.Resolve<MenuSelection>(),
            PageServices.Resolve<CartViewModel>())
    {
    }

    public ItemPage(ItemViewModel vm, MenuSelection selection, CartViewModel cart)
    {
        InitializeComponent();
        _vm = vm;
        _selection = selection;
        _cart = cart;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemViewModel.PriceLabel))
            {
                PriceLabel.Text = _vm.PriceLabel;
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var product = _selection.Product;
        if (product is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _vm.Load(product);
        TitleLabel.Text = _vm.Title;
        PriceLabel.Text = _vm.PriceLabel;
        DescriptionLabel.Text = product.Description ?? "";
        QuantityLabel.Text = _vm.Quantity.ToString();
        NotesEntry.Text = _vm.Notes;
        ErrorLabel.Text = "";
        BuildGroups();
    }

    private void BuildGroups()
    {
        GroupsStack.Children.Clear();
        foreach (var group in _vm.Groups)
        {
            GroupsStack.Children.Add(new Label
            {
                Text = group.Caption,
                TextColor = Color.FromArgb("#0F172A"),
                FontAttributes = FontAttributes.Bold,
                FontSize = 16
            });

            foreach (var option in group.Options)
            {
                var check = new CheckBox { Color = Color.FromArgb("#0F766E") };
                check.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(ModifierOption.IsSelected), source: option));
                var row = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        check,
                        new Label
                        {
                            Text = option.Label,
                            TextColor = Color.FromArgb("#0F172A"),
                            FontSize = 16,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                };
                GroupsStack.Children.Add(row);
            }
        }
    }

    private void OnMinus(object? sender, EventArgs e)
    {
        _vm.Quantity--;
        QuantityLabel.Text = _vm.Quantity.ToString();
        PriceLabel.Text = _vm.PriceLabel;
    }

    private void OnPlus(object? sender, EventArgs e)
    {
        _vm.Quantity++;
        QuantityLabel.Text = _vm.Quantity.ToString();
        PriceLabel.Text = _vm.PriceLabel;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        _vm.Notes = NotesEntry.Text ?? "";
        var error = _vm.TryAddToCart(_cart.Cart);
        if (error is not null)
        {
            ErrorLabel.Text = error;
            return;
        }

        _cart.RefreshTotals();
        await Shell.Current.GoToAsync("//cart");
    }
}

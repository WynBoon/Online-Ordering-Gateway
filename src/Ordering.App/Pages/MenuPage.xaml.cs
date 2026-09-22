using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class MenuPage : ContentPage
{
    private static readonly Color ChipOn = Color.FromArgb("#0F766E");
    private static readonly Color ChipOff = Color.FromArgb("#E2E8F0");
    private static readonly Color ChipOnText = Colors.White;
    private static readonly Color ChipOffText = Color.FromArgb("#334155");

    private readonly MenuViewModel _vm;
    private readonly MenuSelection _selection;

    public MenuPage()
        : this(PageServices.Resolve<MenuViewModel>(), PageServices.Resolve<MenuSelection>())
    {
    }

    public MenuPage(MenuViewModel vm, MenuSelection selection)
    {
        InitializeComponent();
        _vm = vm;
        _selection = selection;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await _vm.EnsureConnectedAsync())
        {
            await Shell.Current.GoToAsync("//connect");
            return;
        }

        await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        await _vm.LoadAsync();
        ErrorLabel.Text = _vm.Error ?? "";
        BindCategories();
        BindProducts();
    }

    private void BindCategories()
    {
        CategoryBar.Children.Clear();
        foreach (var name in _vm.Categories)
        {
            var selected = string.Equals(name, _vm.SelectedCategory, StringComparison.OrdinalIgnoreCase);
            var chip = new Button
            {
                Text = name,
                HeightRequest = 36,
                Padding = new Thickness(14, 0),
                CornerRadius = 18,
                FontSize = 14,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                BackgroundColor = selected ? ChipOn : ChipOff,
                TextColor = selected ? ChipOnText : ChipOffText
            };
            chip.Clicked += (_, _) => OnCategoryClicked(name);
            CategoryBar.Children.Add(chip);
        }
    }

    private void OnCategoryClicked(string name)
    {
        _vm.SelectCategory(name);
        BindCategories();
        BindProducts();
    }

    private void BindProducts()
    {
        ProductsView.ItemsSource = null;
        ProductsView.ItemsSource = _vm.Products;
        ProductsView.SelectedItem = null;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadMenuAsync();

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            await LoadMenuAsync();
        }
        finally
        {
            if (sender is RefreshView refresh)
            {
                refresh.IsRefreshing = false;
            }
        }
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        await _vm.DisconnectAsync();
        await Shell.Current.GoToAsync("//connect");
    }

    private async void OnProductSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MenuProductRow row)
        {
            return;
        }

        _selection.Product = row.Product;
        await Shell.Current.GoToAsync("item");
    }
}

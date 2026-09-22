using Ordering.ViewModels;

namespace Ordering.App.Pages;

public partial class LabPage : ContentPage
{
    private readonly LabViewModel _vm;

    public LabPage()
        : this(PageServices.Resolve<LabViewModel>())
    {
    }

    public LabPage(LabViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    private async void OnReplay(object? sender, EventArgs e)
    {
        await _vm.ReplayIdempotencyAsync();
        ResultLabel.Text = _vm.Result ?? "";
    }

    private async void OnUnknownPlu(object? sender, EventArgs e)
    {
        await _vm.UnknownPluAsync();
        ResultLabel.Text = _vm.Result ?? "";
    }

    private async void OnPaused(object? sender, EventArgs e)
    {
        await _vm.PausedStoreAsync();
        ResultLabel.Text = _vm.Result ?? "";
    }
}

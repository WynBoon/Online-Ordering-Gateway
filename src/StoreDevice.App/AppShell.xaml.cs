namespace StoreDevice.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("order", typeof(Pages.OrderPage));
    }
}

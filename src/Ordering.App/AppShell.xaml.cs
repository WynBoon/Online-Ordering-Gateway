namespace Ordering.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("item", typeof(Pages.ItemPage));
        Routing.RegisterRoute("tracking", typeof(Pages.TrackingPage));
    }
}

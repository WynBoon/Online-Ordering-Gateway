using Android.App;
using Android.Runtime;

namespace StoreDevice.App;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp()
	{
		Java.Lang.JavaSystem.SetProperty("java.net.preferIPv4Stack", "true");
		Java.Lang.JavaSystem.SetProperty("java.net.preferIPv6Addresses", "false");
		return MauiProgram.CreateMauiApp();
	}
}

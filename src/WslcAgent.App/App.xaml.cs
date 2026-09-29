namespace WslcAgent.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
#if ANDROID
        // With the window insets owned by MainActivity, resize keeps the last
        // fields above the virtual keyboard instead of panning the page.
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(
            this,
            Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "WSLC AI Client" };
    }
}

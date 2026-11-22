using System.Windows;
using ParentalControl.Admin.Helpers;
using ParentalControl.Core.Data;

namespace ParentalControl.Admin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DatabaseManager.Initialize();
        // Light or dark like Windows apps, also when the Windows setting changes while the app runs
        SystemThemeHelper.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemThemeHelper.Stop();
        base.OnExit(e);
    }
}

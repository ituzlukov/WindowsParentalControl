using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace ParentalControl.Admin.Helpers;

/// <summary>
/// Follows the Windows app theme (Settings → Personalization → Colors → "Choose your app mode"):
/// light or dark Material Design theme and a matching window title bar, updated live when the setting changes.
/// </summary>
public static class SystemThemeHelper
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmwaUseImmersiveDarkMode = 20;
    private static readonly System.Windows.Media.Color IndigoDefault = System.Windows.Media.Color.FromRgb(0x3F, 0x51, 0xB5); // Indigo 500
    private static readonly System.Windows.Media.Color IndigoLight = System.Windows.Media.Color.FromRgb(0x9F, 0xA8, 0xDA);   // Indigo 200

    public static void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Stop() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    /// <summary>Applies the current Windows app theme to the app and its windows.</summary>
    public static void Apply()
    {
        var dark = AppsUseDarkTheme();

        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        // Indigo 500 is too dark for text and icons on a dark background; dark themes use a lighter
        // shade of the primary color (Material Design guidance), with dark text on primary surfaces
        theme.SetPrimaryColor(dark ? IndigoLight : IndigoDefault);
        paletteHelper.SetTheme(theme);

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window, dark);
        }
    }

    /// <summary>Dark or light title bar for a window; call once its handle exists (SourceInitialized).</summary>
    public static void ApplyTitleBar(Window window) => ApplyTitleBar(window, AppsUseDarkTheme());

    private static void ApplyTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var value = dark ? 1 : 0;
        // Supported on Windows 10 20H1+ and Windows 11; ignored elsewhere
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private static bool AppsUseDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        Application.Current?.Dispatcher.BeginInvoke(Apply);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

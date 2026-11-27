using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ParentalControl.Notify;

/// <summary>A message with large text and an OK button, on top of other windows, in the system light or dark theme.</summary>
public sealed class MessageWindow : Window
{
    private readonly bool _dark = IsDarkTheme();

    public MessageWindow(string title, string message)
    {
        var background = _dark ? Color.FromRgb(0x20, 0x20, 0x20) : Colors.White;
        var foreground = _dark ? Colors.White : Color.FromRgb(0x1A, 0x1A, 0x1A);
        var buttonBackground = _dark ? Color.FromRgb(0x9F, 0xA8, 0xDA) : Color.FromRgb(0x3F, 0x51, 0xB5); // Indigo, as in the admin UI
        var buttonForeground = _dark ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Colors.White;

        Title = title;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.ico"));
        Width = 760;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;
        Background = new SolidColorBrush(background);

        var text = new TextBlock
        {
            Text = message,
            FontSize = 30,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(foreground),
            Margin = new Thickness(0, 0, 0, 32)
        };

        var ok = new Button
        {
            Content = "OK",
            FontSize = 26,
            MinWidth = 160,
            Padding = new Thickness(24, 10, 24, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true,
            Background = new SolidColorBrush(buttonBackground),
            Foreground = new SolidColorBrush(buttonForeground),
            Template = FlatButtonTemplate()
        };
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(40, 36, 40, 32),
            Children = { text, ok }
        };

        SourceInitialized += (_, _) => UseDarkTitleBar();
        Loaded += (_, _) =>
        {
            PlaceInUpperLeftQuarter();
            Activate();
            ok.Focus();
            // The same sound as the system warning message box
            System.Media.SystemSounds.Exclamation.Play();
        };
    }

    /// <summary>
    /// Centered on the center of the upper-left quarter of the primary screen's work area, so it does not cover
    /// the middle of the screen; kept inside the work area on small screens.
    /// </summary>
    private void PlaceInUpperLeftQuarter()
    {
        const double margin = 16;
        var area = SystemParameters.WorkArea;
        var left = area.Left + area.Width / 4 - ActualWidth / 2;
        var top = area.Top + area.Height / 4 - ActualHeight / 2;
        Left = Math.Max(area.Left + margin, Math.Min(left, area.Right - ActualWidth - margin));
        Top = Math.Max(area.Top + margin, Math.Min(top, area.Bottom - ActualHeight - margin));
    }

    /// <summary>A flat button keeping its colors on hover (the default template turns light blue).</summary>
    private static ControlTemplate FlatButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.PaddingProperty,
            new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static bool IsDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void UseDarkTitleBar()
    {
        if (!_dark) return;
        var value = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

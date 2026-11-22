using System.Windows;
using ParentalControl.Admin.Helpers;
using ParentalControl.Admin.ViewModels;

namespace ParentalControl.Admin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => SystemThemeHelper.ApplyTitleBar(this);
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.StopServicePolling();
    }
}

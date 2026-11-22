using CommunityToolkit.Mvvm.ComponentModel;

namespace ParentalControl.Admin.ViewModels;

/// <summary>
/// One weekday in the per-day overrides table. An empty field means "use the default value".
/// </summary>
public partial class DayOverrideRow : ObservableObject
{
    public DayOverrideRow(DayOfWeek day)
    {
        Day = day;
    }

    public DayOfWeek Day { get; }

    public string DayName => Day.ToString();

    [ObservableProperty]
    private string _dailyMinutes = string.Empty;

    [ObservableProperty]
    private string _scheduleStart = string.Empty;

    [ObservableProperty]
    private string _scheduleEnd = string.Empty;

    public void Clear()
    {
        DailyMinutes = string.Empty;
        ScheduleStart = string.Empty;
        ScheduleEnd = string.Empty;
    }
}

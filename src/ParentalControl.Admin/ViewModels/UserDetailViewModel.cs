using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class UserDetailViewModel : ObservableObject
{
    private const int DefaultDailyMinutes = 120;
    private const string DefaultScheduleStart = "08:00";
    private const string DefaultScheduleEnd = "22:00";

    private readonly Dictionary<string, string> _sidToUsername;
    private readonly Action _navigateBack;

    public UserRow User { get; }

    [ObservableProperty]
    private int _dailyMinutes = DefaultDailyMinutes;

    [ObservableProperty]
    private string _scheduleStart = DefaultScheduleStart;

    [ObservableProperty]
    private string _scheduleEnd = DefaultScheduleEnd;

    /// <summary>Null while the field is empty; an empty field is saved as 0.</summary>
    [ObservableProperty]
    private int? _editableUsageMinutes;

    /// <summary>Custom bonus to add, next to the +15 / +30 / +60 buttons.</summary>
    [ObservableProperty]
    private int? _customBonusMinutes;

    [ObservableProperty]
    private ObservableCollection<EventRecord> _eventRecords = [];

    /// <summary>
    /// Per-weekday overrides, Monday first.
    /// </summary>
    public ObservableCollection<DayOverrideRow> DayOverrides { get; } = new(
        new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        }.Select(d => new DayOverrideRow(d)));

    public UserDetailViewModel(UserRow user, Dictionary<string, string> sidToUsername, Action navigateBack)
    {
        User = user;
        _sidToUsername = sidToUsername;
        _navigateBack = navigateBack;
    }

    public void LoadAll()
    {
        RefreshUser();
        LoadLimitFields();
        RefreshEvents();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateBack();
    }

    [RelayCommand]
    private void RefreshUser()
    {
        User.RefreshUsage();
        User.RefreshLimits();
        LoadLimitFields();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void RefreshEvents()
    {
        var events = EventRepository.GetEvents(userSid: User.Sid);
        foreach (var evt in events)
        {
            evt.Username = _sidToUsername.TryGetValue(evt.UserSid, out var name) ? name : evt.UserSid;
        }
        EventRecords = new ObservableCollection<EventRecord>(events);
    }

    [RelayCommand]
    private void SetUsage()
    {
        var minutes = EditableUsageMinutes ?? 0;
        if (minutes < 0)
        {
            MessageBox.Show("Usage minutes cannot be negative.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        UsageRepository.SetUsage(User.Id, today, minutes);
        User.RefreshUsage();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    /// <summary>Grants extra minutes for today on top of the daily limit (button parameter: "15", "30", ...).</summary>
    [RelayCommand]
    private void AddBonus(string minutesText)
    {
        if (!int.TryParse(minutesText, out var minutes) || minutes <= 0) return;
        GrantBonus(minutes);
    }

    [RelayCommand]
    private void AddCustomBonus()
    {
        if (CustomBonusMinutes is not { } minutes || minutes < 1 || minutes > 720)
        {
            MessageBox.Show("Bonus must be between 1 and 720 minutes.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GrantBonus(minutes);
        CustomBonusMinutes = null;
    }

    private void GrantBonus(int minutes)
    {
        var result = BonusGrant.Grant(User.Id, minutes, DateTime.Now);
        EventRepository.LogEvent(User.Sid, EventType.BONUS_GRANTED, BonusGrantDetails(minutes, result));

        User.RefreshUsage();
        User.RefreshLimits();
        RefreshEvents();
    }

    private static string BonusGrantDetails(int minutes, BonusGrantResult result) =>
        $"+{minutes} min (bonus today: {result.TotalBonusMinutes} min)" +
        (result.ScheduleEndExtendedTo is { } end ? $"; allowed until {end:HH\\:mm} today" : "");

    [RelayCommand]
    private void UnlockParentLock()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        UsageRepository.SetParentLock(User.Id, today, false);
        EventRepository.LogEvent(User.Sid, EventType.LOCKED_OUT, "Parent lock lifted in the admin app");

        User.RefreshUsage();
        RefreshEvents();
    }

    [RelayCommand]
    private void SetLimits()
    {
        if (!TimeInput.TryParse(ScheduleStart, out var start) ||
            !TimeInput.TryParse(ScheduleEnd, out var end))
        {
            ShowValidationError("Invalid time format. Use HH:mm or just digits (e.g., 08:00, 800 or 8).");
            return;
        }

        if (start >= end)
        {
            ShowValidationError("Schedule start must be before schedule end.");
            return;
        }

        if (!IsValidDailyMinutes(DailyMinutes))
        {
            ShowValidationError("Daily minutes must be between 1 and 1440 (24 hours).");
            return;
        }

        var defaults = new LimitConfig
        {
            UserId = User.Id,
            DailyMinutes = DailyMinutes,
            ScheduleStart = start,
            ScheduleEnd = end
        };

        var overrides = new List<LimitOverride>();
        foreach (var row in DayOverrides)
        {
            var dayOverride = ParseOverride(row, out var error);
            if (dayOverride is null)
            {
                ShowValidationError($"{row.DayName}: {error}");
                return;
            }

            var effective = defaults.WithOverride(dayOverride);
            if (effective.ScheduleStart >= effective.ScheduleEnd)
            {
                ShowValidationError(
                    $"{row.DayName}: schedule start ({effective.ScheduleStart:HH:mm}) must be before " +
                    $"schedule end ({effective.ScheduleEnd:HH:mm}).");
                return;
            }

            overrides.Add(dayOverride);
        }

        LimitRepository.Save(defaults, overrides);
        UserRepository.SetRestricted(User.Id, true);
        User.IsRestricted = true;
        User.RefreshLimits();
        LoadLimitFields();

        MessageBox.Show("Limits saved successfully.", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RemoveLimits()
    {
        var result = MessageBox.Show(
            $"Remove all limits for {User.Username}? This will allow unrestricted access.",
            "Confirm Remove Limits", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        LimitRepository.Delete(User.Id);
        UserRepository.SetRestricted(User.Id, false);
        User.IsRestricted = false;
        User.RefreshLimits();

        DailyMinutes = DefaultDailyMinutes;
        ScheduleStart = DefaultScheduleStart;
        ScheduleEnd = DefaultScheduleEnd;
        foreach (var row in DayOverrides) row.Clear();
    }

    private void LoadLimitFields()
    {
        var defaults = LimitRepository.GetByUserId(User.Id);
        if (defaults is not null)
        {
            DailyMinutes = defaults.DailyMinutes;
            ScheduleStart = defaults.ScheduleStart.ToString("HH:mm");
            ScheduleEnd = defaults.ScheduleEnd.ToString("HH:mm");
        }
        else
        {
            DailyMinutes = DefaultDailyMinutes;
            ScheduleStart = DefaultScheduleStart;
            ScheduleEnd = DefaultScheduleEnd;
        }

        var overrides = defaults is null ? new List<LimitOverride>() : LimitRepository.GetOverrides(User.Id);
        foreach (var row in DayOverrides)
        {
            var dayOverride = overrides.FirstOrDefault(o => o.DayOfWeek == row.Day);
            row.DailyMinutes = dayOverride?.DailyMinutes?.ToString() ?? string.Empty;
            row.ScheduleStart = dayOverride?.ScheduleStart?.ToString("HH:mm") ?? string.Empty;
            row.ScheduleEnd = dayOverride?.ScheduleEnd?.ToString("HH:mm") ?? string.Empty;
        }
    }

    /// <summary>
    /// Parses a weekday row; empty fields stay null (use the default). Returns null and an error on invalid input.
    /// </summary>
    private LimitOverride? ParseOverride(DayOverrideRow row, out string error)
    {
        error = string.Empty;
        var dayOverride = new LimitOverride { UserId = User.Id, DayOfWeek = row.Day };

        if (!string.IsNullOrWhiteSpace(row.DailyMinutes))
        {
            if (!int.TryParse(row.DailyMinutes.Trim(), out var minutes) || !IsValidDailyMinutes(minutes))
            {
                error = "daily minutes must be between 1 and 1440 (24 hours), or empty to use the default.";
                return null;
            }
            dayOverride.DailyMinutes = minutes;
        }

        if (!string.IsNullOrWhiteSpace(row.ScheduleStart))
        {
            if (!TimeInput.TryParse(row.ScheduleStart.Trim(), out var start))
            {
                error = "invalid schedule start. Use HH:mm or just digits (e.g., 08:00, 800 or 8), or leave empty to use the default.";
                return null;
            }
            dayOverride.ScheduleStart = start;
        }

        if (!string.IsNullOrWhiteSpace(row.ScheduleEnd))
        {
            if (!TimeInput.TryParse(row.ScheduleEnd.Trim(), out var end))
            {
                error = "invalid schedule end. Use HH:mm or just digits (e.g., 22:00, 2200 or 22), or leave empty to use the default.";
                return null;
            }
            dayOverride.ScheduleEnd = end;
        }

        return dayOverride;
    }

    private static bool IsValidDailyMinutes(int minutes) => minutes is >= 1 and <= 1440;

    private static void ShowValidationError(string message)
    {
        MessageBox.Show(message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

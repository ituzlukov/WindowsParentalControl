using CommunityToolkit.Mvvm.ComponentModel;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class UserRow : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _sid = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isRestricted;

    [ObservableProperty]
    private bool _isAdmin;

    public string UserType => IsAdmin ? "Administrator" : "Standard User";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int? _dailyMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    private string? _scheduleStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    private string? _scheduleEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int _todayMinutesUsed;

    /// <summary>Extra minutes granted for today on top of the daily limit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int _todayBonusMinutes;

    public string LimitSummary
    {
        get
        {
            if (!DailyMinutes.HasValue) return "No limits";
            var total = DailyMinutes.Value + TodayBonusMinutes;
            var remaining = Math.Max(0, total - TodayMinutesUsed);
            return $"{FormatMinutes(total)} today ({FormatMinutes(remaining)} remaining)";
        }
    }

    /// <summary>Today's usage against today's allowance (daily limit plus bonus), e.g. "60 min of 180 (120 remaining)".</summary>
    public string UsageSummary
    {
        get
        {
            if (!DailyMinutes.HasValue) return $"{TodayMinutesUsed} min (no limit)";
            var total = DailyMinutes.Value + TodayBonusMinutes;
            var remaining = Math.Max(0, total - TodayMinutesUsed);
            return $"{TodayMinutesUsed} min of {total} ({remaining} remaining)";
        }
    }

    public string ScheduleSummary
    {
        get
        {
            if (ScheduleStart is not null && ScheduleEnd is not null)
                return $"{ScheduleStart} - {ScheduleEnd} today";
            return "No schedule";
        }
    }

    private static string FormatMinutes(int minutes)
    {
        if (minutes < 60) return $"{minutes}min";
        var h = minutes / 60;
        var m = minutes % 60;
        return m > 0 ? $"{h}h {m}min" : $"{h}h";
    }

    public static UserRow ForAdmin(string sid, string username)
    {
        return new UserRow
        {
            Id = -1,
            Sid = sid,
            Username = username,
            IsRestricted = false,
            IsAdmin = true
        };
    }

    public static UserRow FromDatabase(User user)
    {
        var row = new UserRow
        {
            Id = user.Id,
            Sid = user.Sid,
            Username = user.Username,
            IsRestricted = user.IsRestricted,
            IsAdmin = false
        };

        var limit = LimitRepository.GetEffective(user.Id, DateOnly.FromDateTime(DateTime.Now));
        if (limit is not null)
        {
            row.DailyMinutes = limit.DailyMinutes;
            row.ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            row.ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = UsageRepository.GetUsage(user.Id, today);
        if (usage is not null)
        {
            row.TodayMinutesUsed = usage.MinutesUsed;
            row.TodayBonusMinutes = usage.BonusMinutes;
        }

        return row;
    }

    public void RefreshLimits()
    {
        var limit = LimitRepository.GetEffective(Id, DateOnly.FromDateTime(DateTime.Now));
        if (limit is not null)
        {
            DailyMinutes = limit.DailyMinutes;
            ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }
        else
        {
            DailyMinutes = null;
            ScheduleStart = null;
            ScheduleEnd = null;
        }
    }

    public void RefreshUsage()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = UsageRepository.GetUsage(Id, today);
        TodayMinutesUsed = usage?.MinutesUsed ?? 0;
        TodayBonusMinutes = usage?.BonusMinutes ?? 0;
    }
}

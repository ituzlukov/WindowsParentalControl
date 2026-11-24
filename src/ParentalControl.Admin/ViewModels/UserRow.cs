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
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int? _dailyMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    [NotifyPropertyChangedFor(nameof(TodayHours))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    private string? _scheduleStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    [NotifyPropertyChangedFor(nameof(TodayHours))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    private string? _scheduleEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int _todayMinutesUsed;

    /// <summary>Extra minutes granted for today on top of the daily limit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private int _todayBonusMinutes;

    /// <summary>A parent locked the user for the rest of today ("Lock now" in Telegram).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    [NotifyPropertyChangedFor(nameof(RemainingSummary))]
    private bool _todayParentLocked;

    /// <summary>Dashboard card: today's usage against today's allowance, e.g. "1h 10min of 3h used".</summary>
    public string LimitSummary
    {
        get
        {
            if (!DailyMinutes.HasValue) return "No limits";
            var total = DailyMinutes.Value + TodayBonusMinutes;
            return $"{FormatMinutes(TodayMinutesUsed)} of {FormatMinutes(total)} used";
        }
    }

    /// <summary>
    /// Dashboard card, after <see cref="LimitSummary"/> on the same line: the real time remaining today
    /// (see <see cref="RemainingTime"/>), or the parent lock. Empty if the user has no limits.
    /// </summary>
    public string RemainingSummary
    {
        get
        {
            if (!DailyMinutes.HasValue) return "";
            if (TodayParentLocked) return " · locked by parent";
            return $" · {FormatMinutes(RemainingNow(DailyMinutes.Value + TodayBonusMinutes))} remaining";
        }
    }

    /// <summary>
    /// Today's usage against today's allowance (daily limit plus bonus) and what is really left, which can be
    /// less than the allowance if the schedule window ends first, e.g. "60 min of 180 (60 remaining)" at 20:30.
    /// Computed when the user's data is loaded or refreshed.
    /// </summary>
    public string UsageSummary
    {
        get
        {
            if (!DailyMinutes.HasValue) return $"{TodayMinutesUsed} min (no limit)";
            var total = DailyMinutes.Value + TodayBonusMinutes;
            return $"{TodayMinutesUsed} min of {total} ({RemainingNow(total)} remaining)";
        }
    }

    /// <summary>The smaller of the allowance left and the schedule window left (see <see cref="RemainingTime"/>).</summary>
    private int RemainingNow(int allowedMinutes)
    {
        if (ScheduleStart is null || ScheduleEnd is null ||
            !TimeOnly.TryParseExact(ScheduleStart, "HH:mm", out var start) ||
            !TimeOnly.TryParseExact(ScheduleEnd, "HH:mm", out var end))
        {
            return Math.Max(0, allowedMinutes - TodayMinutesUsed);
        }

        return RemainingTime.Minutes(allowedMinutes, TodayMinutesUsed, start, end, TimeOnly.FromDateTime(DateTime.Now));
    }

    public string ScheduleSummary
    {
        get
        {
            if (ScheduleStart is not null && ScheduleEnd is not null)
                return $"{ScheduleStart} – {ScheduleEnd} today";
            return "No schedule";
        }
    }

    /// <summary>Today's allowed hours, e.g. "07:00 – 21:45" (weekday schedule, extended if a bonus moved the end).</summary>
    public string TodayHours =>
        ScheduleStart is not null && ScheduleEnd is not null ? $"{ScheduleStart} – {ScheduleEnd}" : "No schedule";

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
            row.TodayParentLocked = usage.ParentLocked;
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
        TodayParentLocked = usage?.ParentLocked ?? false;
    }
}

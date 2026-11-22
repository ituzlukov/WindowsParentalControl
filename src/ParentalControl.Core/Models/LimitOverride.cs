namespace ParentalControl.Core.Models;

/// <summary>
/// Per-weekday override of a user's default limits. A null field means "use the default value".
/// </summary>
public sealed class LimitOverride
{
    public int UserId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int? DailyMinutes { get; set; }
    public TimeOnly? ScheduleStart { get; set; }
    public TimeOnly? ScheduleEnd { get; set; }

    public bool IsEmpty => DailyMinutes is null && ScheduleStart is null && ScheduleEnd is null;
}

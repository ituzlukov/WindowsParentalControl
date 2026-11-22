namespace ParentalControl.Core.Models;

public sealed class LimitConfig
{
    public int UserId { get; set; }
    public int DailyMinutes { get; set; }
    public TimeOnly ScheduleStart { get; set; }
    public TimeOnly ScheduleEnd { get; set; }

    /// <summary>
    /// The limits in effect on a day: each field comes from the override if it is set there,
    /// otherwise from these (default) limits.
    /// </summary>
    public LimitConfig WithOverride(LimitOverride? dayOverride)
    {
        return new LimitConfig
        {
            UserId = UserId,
            DailyMinutes = dayOverride?.DailyMinutes ?? DailyMinutes,
            ScheduleStart = dayOverride?.ScheduleStart ?? ScheduleStart,
            ScheduleEnd = dayOverride?.ScheduleEnd ?? ScheduleEnd
        };
    }
}

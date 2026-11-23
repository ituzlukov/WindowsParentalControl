namespace ParentalControl.Core.Models;

public sealed class UsageRecord
{
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public int MinutesUsed { get; set; }

    /// <summary>Extra minutes granted for this day on top of the daily limit.</summary>
    public int BonusMinutes { get; set; }

    /// <summary>
    /// Minutes allowed on this day: the daily limit plus the bonus granted for the day (if any).
    /// </summary>
    public static int AllowedMinutes(LimitConfig limit, UsageRecord? usage) =>
        limit.DailyMinutes + (usage?.BonusMinutes ?? 0);
}

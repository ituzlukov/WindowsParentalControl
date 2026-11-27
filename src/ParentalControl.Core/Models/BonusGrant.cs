using ParentalControl.Core.Data;

namespace ParentalControl.Core.Models;

public sealed record BonusGrantResult(int TotalBonusMinutes, TimeOnly? ScheduleEndExtendedTo);

/// <summary>
/// Granting bonus time: "+N" gives the user N minutes more than they had left. What they had left ends at
/// the earlier of two moments, the end of the schedule window and the moment the daily allowance runs out;
/// that moment moves N minutes later. The allowance grows by N, and if the new moment is past the window
/// end, the window end for that day moves to it. So a bonus granted in the evening, when the window is what
/// stops the user, extends the window; one granted in the afternoon, when the allowance is, leaves it alone.
/// </summary>
public static class BonusGrant
{
    private static readonly TimeOnly LastMinuteOfDay = new(23, 59);

    public static BonusGrantResult Grant(int userId, int minutes, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var limit = LimitRepository.GetEffective(userId, today); // already includes today's extension, if any
        TimeOnly? extendTo = null;
        if (limit is not null)
        {
            var usage = UsageRepository.GetUsage(userId, today);
            var allowanceLeft = Math.Max(0, UsageRecord.AllowedMinutes(limit, usage) - (usage?.MinutesUsed ?? 0));
            extendTo = ExtendedScheduleEnd(limit.ScheduleEnd, allowanceLeft, now, minutes);
        }

        var total = UsageRepository.AddBonusMinutes(userId, today, minutes, extendTo);
        return new BonusGrantResult(total, extendTo);
    }

    /// <summary>
    /// Taking time away, the mirror of <see cref="Grant"/>: "−N" leaves the user N minutes less than they
    /// really have left (allowance and window), but not less than zero. Only the allowance shrinks; the window
    /// stays. Returns the minutes actually taken: fewer than N if less was left, 0 if nothing was.
    /// </summary>
    public static int Take(int userId, int minutes, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var limit = LimitRepository.GetEffective(userId, today);
        if (limit is null) return 0;

        var usage = UsageRepository.GetUsage(userId, today);
        var allowed = UsageRecord.AllowedMinutes(limit, usage);
        var used = usage?.MinutesUsed ?? 0;
        var remaining = RemainingTime.Minutes(allowed, used, limit.ScheduleStart, limit.ScheduleEnd, TimeOnly.FromDateTime(now));

        var reduceBy = AllowanceReduction(allowed, used, remaining, minutes);
        if (reduceBy > 0) UsageRepository.TakeMinutes(userId, today, reduceBy);
        return Math.Min(minutes, remaining);
    }

    /// <summary>
    /// How much to cut the allowance so that <paramref name="remaining"/> drops by <paramref name="minutes"/>
    /// (to no less than zero): the allowance is cut to used + the new remaining.
    /// </summary>
    public static int AllowanceReduction(int allowed, int used, int remaining, int minutes)
    {
        if (remaining <= 0 || minutes <= 0) return 0; // nothing left to take
        var newRemaining = Math.Max(0, remaining - minutes);
        return Math.Max(0, allowed - (used + newRemaining));
    }

    /// <summary>
    /// The new window end, or null if the window does not need to move.
    /// The user would stop at max(now, min(window end, now + allowance left)); with the bonus they stop
    /// <paramref name="bonusMinutes"/> later. Rounded up to the minute, at most 23:59 (never crosses midnight).
    /// </summary>
    public static TimeOnly? ExtendedScheduleEnd(TimeOnly currentEnd, int allowanceLeftMinutes, DateTime now, int bonusMinutes)
    {
        var windowEnd = now.Date + currentEnd.ToTimeSpan();
        var allowanceEnd = now.AddMinutes(Math.Max(0, allowanceLeftMinutes));
        var stopAt = windowEnd < allowanceEnd ? windowEnd : allowanceEnd;
        if (stopAt < now) stopAt = now; // already past the window end

        var until = stopAt.AddMinutes(bonusMinutes);
        if (until.Second > 0 || until.Millisecond > 0)
        {
            until = until.AddSeconds(60 - until.Second).AddMilliseconds(-until.Millisecond);
        }

        var candidate = until.Date > now.Date ? LastMinuteOfDay : new TimeOnly(until.Hour, until.Minute);
        return candidate > currentEnd ? candidate : null;
    }

    /// <summary>The day's window end: the scheduled end, or the extended one if a bonus moved it later.</summary>
    public static TimeOnly EffectiveScheduleEnd(TimeOnly scheduledEnd, TimeOnly? extendedEnd) =>
        extendedEnd is { } extended && extended > scheduledEnd ? extended : scheduledEnd;
}

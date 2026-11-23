namespace ParentalControl.Service;

/// <summary>
/// Decides when to warn a restricted user that they are about to be logged off.
/// </summary>
public static class UsageWarnings
{
    /// <summary>Warning thresholds in minutes before logoff, largest first.</summary>
    public static readonly IReadOnlyList<int> ThresholdsMinutes = [10, 1];

    /// <summary>
    /// Minutes until the user is logged off: whichever comes first, the allowed minutes for today
    /// (daily limit plus bonus) or the end of the schedule window. Assumes <paramref name="now"/> is inside the window.
    /// </summary>
    public static int GetRemainingMinutes(int allowedMinutes, int minutesUsed, TimeOnly scheduleEnd, TimeOnly now)
    {
        var byLimit = allowedMinutes - minutesUsed;
        var byWindow = (int)Math.Ceiling((scheduleEnd.ToTimeSpan() - now.ToTimeSpan()).TotalMinutes);
        return Math.Max(0, Math.Min(byLimit, byWindow));
    }

    /// <summary>
    /// Each threshold is warned about once. <paramref name="lastWarnedThreshold"/> is the smallest threshold
    /// already warned about in this session (null if none).
    /// </summary>
    /// <returns>
    /// The threshold to warn about now (null for no warning), and the new state to store for the session.
    /// If remaining time grows again (e.g. the parent granted extra time), the state is raised accordingly,
    /// so the smaller thresholds are warned about again later.
    /// </returns>
    public static (int? WarnThreshold, int? NewState) Evaluate(int remainingMinutes, int? lastWarnedThreshold)
    {
        int? crossed = null;
        foreach (var threshold in ThresholdsMinutes)
        {
            if (remainingMinutes <= threshold) crossed = threshold;
        }

        if (crossed is null) return (null, null);
        if (lastWarnedThreshold is null || crossed < lastWarnedThreshold) return (crossed, crossed);
        return (null, crossed);
    }

    public static (string Title, string Message) BuildMessage(int remainingMinutes)
    {
        var unit = remainingMinutes == 1 ? "minute" : "minutes";
        return ("Parental Control",
            $"Your computer time ends in {remainingMinutes} {unit}.\n\n" +
            "Save your work: you will be logged off automatically.");
    }
}

namespace ParentalControl.Core.Models;

/// <summary>
/// How much time the user really has left today: the smaller of what is left of the allowance (daily limit
/// plus bonus) and what is left of today's schedule window.
/// </summary>
public static class RemainingTime
{
    public static int Minutes(int allowedMinutes, int minutesUsed, TimeOnly scheduleStart, TimeOnly scheduleEnd, TimeOnly now)
    {
        var allowanceLeft = allowedMinutes - minutesUsed;

        int windowLeft;
        if (now >= scheduleEnd)
            windowLeft = 0;
        else if (now < scheduleStart)
            windowLeft = (int)(scheduleEnd - scheduleStart).TotalMinutes; // the whole window is still ahead
        else
            windowLeft = (int)Math.Ceiling((scheduleEnd.ToTimeSpan() - now.ToTimeSpan()).TotalMinutes);

        return Math.Max(0, Math.Min(allowanceLeft, windowLeft));
    }
}

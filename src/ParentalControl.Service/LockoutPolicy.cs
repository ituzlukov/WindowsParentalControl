namespace ParentalControl.Service;

public enum LockoutAction
{
    /// <summary>Nothing to do.</summary>
    None,

    /// <summary>More time is available again (bonus granted, schedule changed): end the lockout.</summary>
    LiftLockout,

    /// <summary>Time is up: disconnect the session, keeping its programs running.</summary>
    LockOut,

    /// <summary>The user signed back in while still out of time: tell them and disconnect again.</summary>
    RelockAfterNotice,

    /// <summary>Still out of time after the grace period: log the session off.</summary>
    Logoff
}

/// <summary>
/// When time is up, the session is first locked (disconnected) rather than logged off, so open work and
/// games are not lost and the user can continue if the parent grants more time. If no time is granted
/// within <see cref="GracePeriod"/>, the session is logged off so that nothing keeps running in it
/// (voice chat, music, downloads).
/// </summary>
public static class LockoutPolicy
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);

    /// <param name="outOfTime">The daily allowance is used up or the schedule window is closed.</param>
    /// <param name="lockedOutAt">When the session was locked out; null if it is not locked out.</param>
    /// <param name="isPaused">The session is locked or disconnected, i.e. nobody is using it.</param>
    public static LockoutAction Decide(bool outOfTime, DateTime? lockedOutAt, bool isPaused, DateTime now)
    {
        if (!outOfTime) return lockedOutAt is null ? LockoutAction.None : LockoutAction.LiftLockout;
        if (lockedOutAt is null) return LockoutAction.LockOut;
        if (now - lockedOutAt.Value >= GracePeriod) return LockoutAction.Logoff;
        return isPaused ? LockoutAction.None : LockoutAction.RelockAfterNotice;
    }
}

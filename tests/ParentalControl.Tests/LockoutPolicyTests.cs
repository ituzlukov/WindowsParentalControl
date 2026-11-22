using ParentalControl.Service;

namespace ParentalControl.Tests;

public class LockoutPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 21, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimeLeft_NotLockedOut_NothingToDo(bool isPaused)
    {
        Assert.Equal(LockoutAction.None, LockoutPolicy.Decide(outOfTime: false, lockedOutAt: null, isPaused, Now));
    }

    [Fact]
    public void TimeUp_LocksOut()
    {
        Assert.Equal(LockoutAction.LockOut, LockoutPolicy.Decide(outOfTime: true, lockedOutAt: null, isPaused: false, Now));
    }

    [Fact]
    public void TimeUp_WhileUserLockedSessionThemselves_StillLocksOut()
    {
        // The session is disconnected so the grace period starts and ends with a logoff
        Assert.Equal(LockoutAction.LockOut, LockoutPolicy.Decide(outOfTime: true, lockedOutAt: null, isPaused: true, Now));
    }

    [Fact]
    public void LockedOut_SessionIdle_WaitsForGracePeriod()
    {
        var lockedOutAt = Now.AddMinutes(-5);

        Assert.Equal(LockoutAction.None, LockoutPolicy.Decide(outOfTime: true, lockedOutAt, isPaused: true, Now));
    }

    [Fact]
    public void LockedOut_UserSignedBackIn_Relocks()
    {
        var lockedOutAt = Now.AddMinutes(-5);

        Assert.Equal(LockoutAction.RelockAfterNotice, LockoutPolicy.Decide(outOfTime: true, lockedOutAt, isPaused: false, Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedOut_GracePeriodOver_LogsOff(bool isPaused)
    {
        var lockedOutAt = Now - LockoutPolicy.GracePeriod;

        Assert.Equal(LockoutAction.Logoff, LockoutPolicy.Decide(outOfTime: true, lockedOutAt, isPaused, Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedOut_MoreTimeGranted_LiftsLockout(bool isPaused)
    {
        var lockedOutAt = Now.AddMinutes(-10);

        Assert.Equal(LockoutAction.LiftLockout, LockoutPolicy.Decide(outOfTime: false, lockedOutAt, isPaused, Now));
    }

    [Fact]
    public void GracePeriod_Is15Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), LockoutPolicy.GracePeriod);
    }
}

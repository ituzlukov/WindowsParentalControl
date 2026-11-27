using ParentalControl.Core.Models;
using ParentalControl.Service.Telegram;

namespace ParentalControl.Tests;

public class BotUiTests
{
    private static readonly LimitConfig Limit = new()
    {
        UserId = 3,
        DailyMinutes = 180,
        ScheduleStart = new TimeOnly(7, 0),
        ScheduleEnd = new TimeOnly(21, 0)
    };

    public static TheoryData<BotAction> Actions => new()
    {
        new GrantBonusAction(3, 30),
        new LockNowAction(3),
        new UnlockAction(12),
        new TakeTimeAction(3, 60)
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public void CallbackData_RoundTrips(BotAction action)
    {
        var data = BotUi.Format(action);

        Assert.True(data.Length <= 64);
        Assert.Equal(action, BotUi.Parse(data));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x:1")]
    [InlineData("b:1")]
    [InlineData("b:1:0")]
    [InlineData("b:1:-5")]
    [InlineData("b:1:999")]
    [InlineData("l:abc")]
    [InlineData("l:1:2")]
    [InlineData("r:1:15")]
    [InlineData("t:1:0")]
    [InlineData("t:1:999")]
    public void InvalidCallbackData_Rejected(string? data)
    {
        Assert.Null(BotUi.Parse(data));
    }

    [Fact]
    public void Keyboard_HasAddAndTakeButtons_AndLockOrUnlock()
    {
        var unlocked = BotUi.UserKeyboard(3, parentLocked: false);
        Assert.Equal(["+15 min", "+30 min", "+60 min"], unlocked[0].Select(b => b.Text));
        Assert.Equal(["−15 min", "−30 min", "−60 min"], unlocked[1].Select(b => b.Text));
        Assert.Equal(new TakeTimeAction(3, 30), BotUi.Parse(unlocked[1][1].CallbackData));
        Assert.Equal("Lock now", unlocked[2][0].Text);

        var locked = BotUi.UserKeyboard(3, parentLocked: true);
        Assert.Equal("Unlock", locked[2][0].Text);
        Assert.Equal(new UnlockAction(3), BotUi.Parse(locked[2][0].CallbackData));
    }

    [Fact]
    public void Status_ShowsTimeTakenAway()
    {
        var usage = new UsageRecord { MinutesUsed = 60, BonusMinutes = -30 };

        var text = BotUi.FormatStatus("Roma", Limit, usage, SessionState.Active, null, new DateTime(2026, 11, 23, 15, 0, 0));

        Assert.Contains("Used 1h of 2h 30min (30min taken away), 1h 30min remaining", text);
    }

    [Fact]
    public void Status_ActiveSession_ShowsUsageAndTimeRemaining()
    {
        var usage = new UsageRecord { MinutesUsed = 130, BonusMinutes = 30 };

        var text = BotUi.FormatStatus("Roma", Limit, usage, SessionState.Active, null, new DateTime(2026, 11, 23, 15, 0, 0));

        Assert.Equal(
            "Roma\n" +
            "Used 2h 10min of 3h 30min (incl. 30min bonus), 1h 20min remaining\n" +
            "Allowed hours today: 07:00-21:00\n" +
            "Session: active",
            text);
    }

    [Fact]
    public void Status_LockedOut_ShowsLogoffTime()
    {
        var usage = new UsageRecord { MinutesUsed = 180 };
        var lockedOutAt = new DateTime(2026, 11, 23, 18, 0, 0);

        var text = BotUi.FormatStatus("Roma", Limit, usage, SessionState.LockedOut, lockedOutAt, lockedOutAt.AddMinutes(2));

        Assert.Contains("0min remaining", text);
        Assert.Contains("Session: locked out since 18:00, logoff at 18:15 unless you add time", text);
    }

    [Fact]
    public void Status_OutsideWindow_AndParentLock()
    {
        var usage = new UsageRecord { MinutesUsed = 10, ParentLocked = true };

        var text = BotUi.FormatStatus("Roma", Limit, usage, SessionState.NotSignedIn, null, new DateTime(2026, 11, 23, 22, 0, 0));

        Assert.Contains("outside allowed hours", text);
        Assert.Contains("Session: not signed in", text);
        Assert.EndsWith("Locked by parent for today", text);
    }
}

using ParentalControl.Core.Models;
using ParentalControl.Service;

namespace ParentalControl.Tests;

public class UsageWarningsTests
{
    private static LimitConfig Limit(int dailyMinutes, int endHour, int endMinute = 0) => new()
    {
        DailyMinutes = dailyMinutes,
        ScheduleStart = new TimeOnly(7, 0),
        ScheduleEnd = new TimeOnly(endHour, endMinute)
    };

    [Fact]
    public void Remaining_ByDailyLimit_WhenItComesFirst()
    {
        var remaining = UsageWarnings.GetRemainingMinutes(Limit(180, 21), minutesUsed: 175, new TimeOnly(15, 0));

        Assert.Equal(5, remaining);
    }

    [Fact]
    public void Remaining_ByScheduleEnd_WhenItComesFirst()
    {
        var remaining = UsageWarnings.GetRemainingMinutes(Limit(180, 21), minutesUsed: 0, new TimeOnly(20, 52));

        Assert.Equal(8, remaining);
    }

    [Fact]
    public void Remaining_ByScheduleEnd_RoundsPartialMinutesUp()
    {
        var remaining = UsageWarnings.GetRemainingMinutes(Limit(180, 21), minutesUsed: 0, new TimeOnly(20, 59, 10));

        Assert.Equal(1, remaining);
    }

    [Fact]
    public void Remaining_NeverNegative()
    {
        var remaining = UsageWarnings.GetRemainingMinutes(Limit(60, 21), minutesUsed: 90, new TimeOnly(12, 0));

        Assert.Equal(0, remaining);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(11)]
    public void NoWarning_AboveTenMinutes(int remaining)
    {
        var (warn, state) = UsageWarnings.Evaluate(remaining, lastWarnedThreshold: null);

        Assert.Null(warn);
        Assert.Null(state);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(5)]
    [InlineData(2)]
    public void TenMinuteWarning_ShownOnce(int remaining)
    {
        var (warn, state) = UsageWarnings.Evaluate(remaining, lastWarnedThreshold: null);
        Assert.Equal(10, warn);
        Assert.Equal(10, state);

        var (again, stateAgain) = UsageWarnings.Evaluate(remaining, state);
        Assert.Null(again);
        Assert.Equal(10, stateAgain);
    }

    [Fact]
    public void OneMinuteWarning_AfterTenMinuteWarning()
    {
        var (warn, state) = UsageWarnings.Evaluate(1, lastWarnedThreshold: 10);
        Assert.Equal(1, warn);
        Assert.Equal(1, state);

        var (again, _) = UsageWarnings.Evaluate(1, state);
        Assert.Null(again);
    }

    [Fact]
    public void LoginWithOneMinuteLeft_ShowsOnlyOneMinuteWarning()
    {
        var (warn, state) = UsageWarnings.Evaluate(1, lastWarnedThreshold: null);

        Assert.Equal(1, warn);
        Assert.Equal(1, state);
    }

    [Fact]
    public void ExtraTimeGranted_RearmsSmallerThresholds()
    {
        // Warned at 1 minute, then the parent granted extra time: 6 minutes left now
        var (warn, state) = UsageWarnings.Evaluate(6, lastWarnedThreshold: 1);
        Assert.Null(warn);
        Assert.Equal(10, state);

        // ... so the 1-minute warning is shown again when the new deadline approaches
        var (oneMinute, _) = UsageWarnings.Evaluate(1, state);
        Assert.Equal(1, oneMinute);
    }

    [Fact]
    public void ExtraTimeGranted_AboveTenMinutes_ResetsState()
    {
        var (warn, state) = UsageWarnings.Evaluate(45, lastWarnedThreshold: 1);

        Assert.Null(warn);
        Assert.Null(state);
    }

    [Fact]
    public void Message_ShowsRemainingMinutes()
    {
        var (title, one) = UsageWarnings.BuildMessage(1);
        var (_, ten) = UsageWarnings.BuildMessage(10);

        Assert.Equal("Parental Control", title);
        Assert.Contains("1 minute.", one);
        Assert.Contains("10 minutes.", ten);
    }
}

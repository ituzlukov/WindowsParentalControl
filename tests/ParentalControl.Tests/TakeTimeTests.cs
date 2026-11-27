using ParentalControl.Core.Models;

namespace ParentalControl.Tests;

/// <summary>"−N" leaves N minutes less than the user really had left, never below zero.</summary>
public class TakeTimeTests
{
    [Theory]
    // allowed, used, remaining, take, expected cut of the allowance
    [InlineData(180, 60, 120, 15, 15)]  // the allowance stops the user: cut by exactly N
    [InlineData(180, 60, 30, 15, 105)]  // the window stops them (30 left): allowance cut to used + 15
    [InlineData(180, 60, 10, 15, 120)]  // less left than N: cut to what was used, time is up
    [InlineData(180, 60, 0, 15, 0)]     // nothing left (e.g. after the window): nothing changes
    [InlineData(180, 180, 0, 30, 0)]
    public void AllowanceReduction(int allowed, int used, int remaining, int take, int expected)
    {
        Assert.Equal(expected, BonusGrant.AllowanceReduction(allowed, used, remaining, take));
    }

    [Fact]
    public void AfterTheCut_RemainingDropsByN()
    {
        // 20:30, window until 21:00 (30 min left), allowance 120 min left
        var allowed = 180;
        var used = 60;
        var now = new TimeOnly(20, 30);
        var start = new TimeOnly(7, 0);
        var end = new TimeOnly(21, 0);
        var remaining = RemainingTime.Minutes(allowed, used, start, end, now);

        var cut = BonusGrant.AllowanceReduction(allowed, used, remaining, 15);

        Assert.Equal(15, RemainingTime.Minutes(allowed - cut, used, start, end, now));
    }
}

using ParentalControl.Core.Models;

namespace ParentalControl.Tests;

public class RemainingTimeTests
{
    private static readonly TimeOnly Start = new(7, 0);
    private static readonly TimeOnly End = new(21, 30);

    [Fact]
    public void Evening_WindowEndsFirst_WindowLeft()
    {
        // 20:30, nothing used of 180 min: only 60 min until the window closes
        Assert.Equal(60, RemainingTime.Minutes(180, 0, Start, End, new TimeOnly(20, 30)));
    }

    [Fact]
    public void Afternoon_AllowanceEndsFirst_AllowanceLeft()
    {
        Assert.Equal(60, RemainingTime.Minutes(180, 120, Start, End, new TimeOnly(15, 0)));
    }

    [Fact]
    public void PartialMinute_CountsAsAMinute()
    {
        Assert.Equal(1, RemainingTime.Minutes(180, 0, Start, End, new TimeOnly(21, 29, 10)));
    }

    [Fact]
    public void AfterTheWindow_Nothing()
    {
        Assert.Equal(0, RemainingTime.Minutes(180, 0, Start, End, new TimeOnly(21, 30)));
        Assert.Equal(0, RemainingTime.Minutes(180, 0, Start, End, new TimeOnly(23, 0)));
    }

    [Fact]
    public void BeforeTheWindow_WholeWindowAhead()
    {
        Assert.Equal(180, RemainingTime.Minutes(180, 0, Start, End, new TimeOnly(6, 0)));
        Assert.Equal(60, RemainingTime.Minutes(180, 0, new TimeOnly(20, 0), new TimeOnly(21, 0), new TimeOnly(6, 0)));
    }

    [Fact]
    public void AllowanceUsedUp_Nothing()
    {
        Assert.Equal(0, RemainingTime.Minutes(180, 200, Start, End, new TimeOnly(12, 0)));
    }
}

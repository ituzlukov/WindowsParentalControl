using ParentalControl.Core.Models;

namespace ParentalControl.Tests;

/// <summary>"+N" gives N minutes more than the user had left, whichever of the window and the allowance stops them.</summary>
public class BonusGrantTests
{
    private static readonly TimeOnly End2130 = new(21, 30);
    private const int Plenty = 180;

    private static DateTime At(int hour, int minute, int second = 0) => new(2026, 11, 23, hour, minute, second);

    [Theory]
    [InlineData(15, 21, 45)]
    [InlineData(30, 22, 0)]
    public void Evening_WindowStopsTheUser_WindowMovesByTheBonus(int bonus, int hour, int minute)
    {
        // 20:30, 3 hours of allowance unused: the window (21:30) is what stops the user
        Assert.Equal(new TimeOnly(hour, minute), BonusGrant.ExtendedScheduleEnd(End2130, Plenty, At(20, 30), bonus));
    }

    [Fact]
    public void TenMinutesBeforeTheEnd_ThirtyMore_UntilTen()
    {
        Assert.Equal(new TimeOnly(22, 0), BonusGrant.ExtendedScheduleEnd(End2130, Plenty, At(21, 20), 30));
    }

    [Fact]
    public void AfterTheEnd_CountsFromNow()
    {
        Assert.Equal(new TimeOnly(22, 10), BonusGrant.ExtendedScheduleEnd(End2130, Plenty, At(21, 40), 30));
    }

    [Fact]
    public void Afternoon_AllowanceStopsTheUser_WindowUnchanged()
    {
        // 15:00, 60 min of allowance left: the user stops at 16:00, with the bonus at 16:30, well before 21:30
        Assert.Null(BonusGrant.ExtendedScheduleEnd(End2130, 60, At(15, 0), 30));
    }

    [Fact]
    public void AllowanceStopsShortlyBeforeTheEnd_WindowMovesPastIt()
    {
        // 21:00, 10 min left: stops at 21:10; +30 means 21:40, past the window end
        Assert.Equal(new TimeOnly(21, 40), BonusGrant.ExtendedScheduleEnd(End2130, 10, At(21, 0), 30));
    }

    [Fact]
    public void AllowanceUsedUp_InTheEvening_CountsFromNow()
    {
        Assert.Equal(new TimeOnly(21, 50), BonusGrant.ExtendedScheduleEnd(End2130, 0, At(21, 20), 30));
    }

    [Fact]
    public void BeforeTheWindowStarts_WindowUnchanged()
    {
        Assert.Null(BonusGrant.ExtendedScheduleEnd(End2130, Plenty, At(6, 30), 30));
    }

    [Fact]
    public void PartialMinute_RoundsUp()
    {
        // Allowance runs out at 21:10:15 (10 min after 21:00:15); +30 -> 21:40:15 -> 21:41
        Assert.Equal(new TimeOnly(21, 41), BonusGrant.ExtendedScheduleEnd(End2130, 10, At(21, 0, 15), 30));
    }

    [Fact]
    public void Extension_NeverCrossesMidnight()
    {
        Assert.Equal(new TimeOnly(23, 59), BonusGrant.ExtendedScheduleEnd(new TimeOnly(23, 30), Plenty, At(23, 20), 60));
    }

    [Fact]
    public void EffectiveEnd_UsesTheLaterOfScheduledAndExtended()
    {
        Assert.Equal(End2130, BonusGrant.EffectiveScheduleEnd(End2130, null));
        Assert.Equal(new TimeOnly(21, 50), BonusGrant.EffectiveScheduleEnd(End2130, new TimeOnly(21, 50)));
        // The weekday schedule was later changed to end after the extension: the schedule wins
        Assert.Equal(new TimeOnly(22, 0), BonusGrant.EffectiveScheduleEnd(new TimeOnly(22, 0), new TimeOnly(21, 50)));
    }
}

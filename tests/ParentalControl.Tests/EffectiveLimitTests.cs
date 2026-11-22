using ParentalControl.Core.Models;

namespace ParentalControl.Tests;

public class EffectiveLimitTests
{
    private static readonly LimitConfig Defaults = new()
    {
        UserId = 7,
        DailyMinutes = 180,
        ScheduleStart = new TimeOnly(7, 0),
        ScheduleEnd = new TimeOnly(21, 0)
    };

    [Fact]
    public void NoOverride_UsesDefaults()
    {
        var effective = Defaults.WithOverride(null);

        Assert.Equal(7, effective.UserId);
        Assert.Equal(180, effective.DailyMinutes);
        Assert.Equal(new TimeOnly(7, 0), effective.ScheduleStart);
        Assert.Equal(new TimeOnly(21, 0), effective.ScheduleEnd);
    }

    [Fact]
    public void FullOverride_ReplacesEveryField()
    {
        var effective = Defaults.WithOverride(new LimitOverride
        {
            DayOfWeek = DayOfWeek.Saturday,
            DailyMinutes = 300,
            ScheduleStart = new TimeOnly(9, 0),
            ScheduleEnd = new TimeOnly(22, 0)
        });

        Assert.Equal(300, effective.DailyMinutes);
        Assert.Equal(new TimeOnly(9, 0), effective.ScheduleStart);
        Assert.Equal(new TimeOnly(22, 0), effective.ScheduleEnd);
    }

    [Fact]
    public void PartialOverride_KeepsDefaultsForNullFields()
    {
        var effective = Defaults.WithOverride(new LimitOverride
        {
            DayOfWeek = DayOfWeek.Friday,
            ScheduleEnd = new TimeOnly(22, 0)
        });

        Assert.Equal(180, effective.DailyMinutes);
        Assert.Equal(new TimeOnly(7, 0), effective.ScheduleStart);
        Assert.Equal(new TimeOnly(22, 0), effective.ScheduleEnd);
    }

    [Fact]
    public void WithOverride_DoesNotModifyDefaults()
    {
        Defaults.WithOverride(new LimitOverride { DailyMinutes = 1 });

        Assert.Equal(180, Defaults.DailyMinutes);
    }

    [Fact]
    public void IsEmpty_OnlyWhenNoFieldIsSet()
    {
        Assert.True(new LimitOverride { DayOfWeek = DayOfWeek.Monday }.IsEmpty);
        Assert.False(new LimitOverride { DailyMinutes = 60 }.IsEmpty);
        Assert.False(new LimitOverride { ScheduleStart = new TimeOnly(8, 0) }.IsEmpty);
        Assert.False(new LimitOverride { ScheduleEnd = new TimeOnly(20, 0) }.IsEmpty);
    }
}

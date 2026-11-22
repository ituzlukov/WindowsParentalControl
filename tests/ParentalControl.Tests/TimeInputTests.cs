using ParentalControl.Core;

namespace ParentalControl.Tests;

public class TimeInputTests
{
    [Theory]
    [InlineData("22:00", 22, 0)]
    [InlineData("7:30", 7, 30)]
    [InlineData("07:05", 7, 5)]
    [InlineData("22.00", 22, 0)]
    [InlineData("22,00", 22, 0)]
    [InlineData("22 00", 22, 0)]
    [InlineData("22-00", 22, 0)]
    [InlineData("22h", 22, 0)]
    [InlineData("22:", 22, 0)]
    [InlineData("2200", 22, 0)]
    [InlineData("0730", 7, 30)]
    [InlineData("730", 7, 30)]
    [InlineData("800", 8, 0)]
    [InlineData("22", 22, 0)]
    [InlineData("7", 7, 0)]
    [InlineData("0", 0, 0)]
    [InlineData("  21  ", 21, 0)]
    [InlineData("23:59", 23, 59)]
    public void Accepts(string text, int hours, int minutes)
    {
        Assert.True(TimeInput.TryParse(text, out var time));
        Assert.Equal(new TimeOnly(hours, minutes), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24")]
    [InlineData("2400")]
    [InlineData("2260")]
    [InlineData("22:60")]
    [InlineData("12345")]
    [InlineData("22:000")]
    [InlineData("ab")]
    [InlineData("22:3x")]
    [InlineData(":30")]
    [InlineData("-5")]
    public void Rejects(string? text)
    {
        Assert.False(TimeInput.TryParse(text, out _));
    }
}

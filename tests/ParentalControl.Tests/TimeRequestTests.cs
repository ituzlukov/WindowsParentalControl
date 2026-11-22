using ParentalControl.Service.Telegram;

namespace ParentalControl.Tests;

public class TimeRequestTests
{
    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(3, 2, 1)]
    [InlineData(3, 3, 0)]
    [InlineData(3, 5, 0)]
    [InlineData(0, 0, 0)]
    public void RequestsLeft_CappedPerDay(int maxPerDay, int usedToday, int expected)
    {
        Assert.Equal(expected, TimeRequestService.RequestsLeft(maxPerDay, usedToday));
    }

    [Fact]
    public void Prompt_ShowsRequestsLeft()
    {
        Assert.Contains("Ask a parent for more time? (requests left today: 2)", TimeRequestService.Prompt(2));
    }
}

using ParentalControl.Core;
using ParentalControl.Service;

namespace ParentalControl.Tests;

public class ChildMessagesTests
{
    [Fact]
    public void AllSamples_HaveText()
    {
        var messages = ChildMessages.AllSamples(LockoutPolicy.GracePeriod);

        Assert.Equal(5, messages.Count);
        Assert.All(messages, m =>
        {
            var text = ChildMessages.Text(m);
            Assert.Equal("Parental Control", text.Title);
            Assert.False(string.IsNullOrWhiteSpace(text.Message));
        });
    }

    [Fact]
    public void English()
    {
        Assert.Contains("ends\nin 1 minute.", ChildMessages.Text(ChildMessage.TimeEndsSoon(1)).Message);
        Assert.Contains("ends\nin 10 minutes.", ChildMessages.Text(ChildMessage.TimeEndsSoon(10)).Message);
        Assert.Contains("after 15 minutes you will be logged off",
            ChildMessages.Text(ChildMessage.TimeIsOver(LockoutPolicy.GracePeriod)).Message);
        Assert.Equal("A parent added 15 minutes.", ChildMessages.Text(ChildMessage.TimeAdded(15)).Message);
        Assert.Equal("A parent took away 15 minutes.", ChildMessages.Text(ChildMessage.TimeTaken(15)).Message);
    }

    [Fact]
    public void Arguments_RoundTrip()
    {
        var message = ChildMessage.TimeTaken(30);

        var args = ChildMessages.ToArguments(message).Split(' ');

        Assert.True(ChildMessages.TryParseArguments(args, out var parsed));
        Assert.Equal(message, parsed);
    }

    [Theory]
    [InlineData]
    [InlineData("TimeEndsSoon")]
    [InlineData("Nonsense", "10")]
    [InlineData("TimeEndsSoon", "-5")]
    [InlineData("42", "10")]
    public void Arguments_Invalid_Rejected(params string[] args)
    {
        Assert.False(ChildMessages.TryParseArguments(args, out _));
    }
}

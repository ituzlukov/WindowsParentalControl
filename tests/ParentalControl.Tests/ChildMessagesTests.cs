using ParentalControl.Core;
using ParentalControl.Service;

namespace ParentalControl.Tests;

public class ChildMessagesTests
{
    [Theory]
    [InlineData("en", "Parental Control")]
    [InlineData("ru", "Родительский контроль")]
    [InlineData("pl", "Kontrola rodzicielska")]
    [InlineData("de", "Parental Control")] // unknown: English
    public void AllSamples_HaveTextInEveryLanguage(string language, string title)
    {
        var messages = ChildMessages.AllSamples(LockoutPolicy.GracePeriod);

        Assert.Equal(5, messages.Count);
        Assert.All(messages, m =>
        {
            var text = ChildMessages.Text(m, language);
            Assert.Equal(title, text.Title);
            Assert.False(string.IsNullOrWhiteSpace(text.Message));
        });
    }

    [Fact]
    public void English()
    {
        Assert.Contains("ends\nin 1 minute.", ChildMessages.Text(ChildMessage.TimeEndsSoon(1), "en").Message);
        Assert.Contains("ends\nin 10 minutes.", ChildMessages.Text(ChildMessage.TimeEndsSoon(10), "en").Message);
        Assert.Contains("after 15 minutes you will be logged off",
            ChildMessages.Text(ChildMessage.TimeIsOver(LockoutPolicy.GracePeriod), "en").Message);
        Assert.Equal("A parent added 15 minutes.", ChildMessages.Text(ChildMessage.TimeAdded(15), "en").Message);
        Assert.Equal("A parent took away 15 minutes.", ChildMessages.Text(ChildMessage.TimeTaken(15), "en").Message);
    }

    [Theory]
    [InlineData(1, "через 1 минуту.")]
    [InlineData(2, "через 2 минуты.")]
    [InlineData(5, "через 5 минут.")]
    [InlineData(10, "через 10 минут.")]
    [InlineData(11, "через 11 минут.")]
    [InlineData(12, "через 12 минут.")]
    [InlineData(21, "через 21 минуту.")]
    [InlineData(22, "через 22 минуты.")]
    public void Russian_PluralForms(int minutes, string expected)
    {
        Assert.Contains(expected, ChildMessages.Text(ChildMessage.TimeEndsSoon(minutes), "ru").Message);
    }

    [Theory]
    [InlineData(1, "za 1 minutę.")]
    [InlineData(2, "za 2 minuty.")]
    [InlineData(5, "za 5 minut.")]
    [InlineData(12, "za 12 minut.")]
    [InlineData(21, "za 21 minut.")]
    [InlineData(22, "za 22 minuty.")]
    public void Polish_PluralForms(int minutes, string expected)
    {
        Assert.Contains(expected, ChildMessages.Text(ChildMessage.TimeEndsSoon(minutes), "pl").Message);
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

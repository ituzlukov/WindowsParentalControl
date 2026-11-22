using ParentalControl.Core.Telegram;

namespace ParentalControl.Tests;

public class TelegramPairingTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("123456")]
    [InlineData(" 123456 ")]
    public void ValidCode_BeforeExpiry_Accepted(string code)
    {
        Assert.True(TelegramSettings.IsPairingCodeValid(code, "123456", Now.AddMinutes(5), Now));
    }

    [Fact]
    public void WrongCode_Rejected()
    {
        Assert.False(TelegramSettings.IsPairingCodeValid("123457", "123456", Now.AddMinutes(5), Now));
        Assert.False(TelegramSettings.IsPairingCodeValid("", "123456", Now.AddMinutes(5), Now));
        Assert.False(TelegramSettings.IsPairingCodeValid("1234567", "123456", Now.AddMinutes(5), Now));
    }

    [Fact]
    public void ExpiredCode_Rejected()
    {
        Assert.False(TelegramSettings.IsPairingCodeValid("123456", "123456", Now, Now));
        Assert.False(TelegramSettings.IsPairingCodeValid("123456", "123456", Now.AddSeconds(-1), Now));
    }
}

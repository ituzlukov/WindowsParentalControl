using System.Globalization;

namespace ParentalControl.Core;

/// <summary>
/// Lenient parsing of times typed by hand, so the colon is optional:
/// "22:00", "22.00", "22 00", "2200", "22", "7", "730" are all accepted.
/// </summary>
public static class TimeInput
{
    private static readonly char[] Separators = [':', '.', ',', ' ', '-', 'h', 'H'];

    public static bool TryParse(string? text, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        string hoursPart;
        string minutesPart;

        var separatorIndex = trimmed.IndexOfAny(Separators);
        if (separatorIndex >= 0)
        {
            hoursPart = trimmed[..separatorIndex];
            minutesPart = trimmed[(separatorIndex + 1)..].Trim();
            if (minutesPart.Length == 0) minutesPart = "0";
        }
        else if (trimmed.Length <= 2)
        {
            // "7", "22": whole hours
            hoursPart = trimmed;
            minutesPart = "0";
        }
        else if (trimmed.Length <= 4)
        {
            // "730", "2200": the last two digits are minutes
            hoursPart = trimmed[..^2];
            minutesPart = trimmed[^2..];
        }
        else
        {
            return false;
        }

        if (!IsDigits(hoursPart) || !IsDigits(minutesPart) || minutesPart.Length > 2) return false;

        var hours = int.Parse(hoursPart, CultureInfo.InvariantCulture);
        var minutes = int.Parse(minutesPart, CultureInfo.InvariantCulture);
        if (hours > 23 || minutes > 59) return false;

        time = new TimeOnly(hours, minutes);
        return true;
    }

    private static bool IsDigits(string s) => s.Length > 0 && s.All(char.IsAsciiDigit);
}

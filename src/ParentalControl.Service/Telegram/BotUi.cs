using System.Globalization;
using ParentalControl.Core.Models;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service.Telegram;

/// <summary>An action requested by pressing an inline button in Telegram.</summary>
public abstract record BotAction;
public sealed record GrantBonusAction(int UserId, int Minutes) : BotAction;
public sealed record LockNowAction(int UserId) : BotAction;
public sealed record UnlockAction(int UserId) : BotAction;
/// <summary>Answer to a child's request for more time; 0 minutes means "no".</summary>
public sealed record AnswerRequestAction(int RequestId, int Minutes) : BotAction;

/// <summary>Where the user's session is, as shown in the status.</summary>
public enum SessionState { NotSignedIn, Active, ScreenLocked, SwitchedAway, LockedOut }

/// <summary>
/// Texts and inline keyboards of the bot, and the compact callback data of its buttons
/// (Telegram allows at most 64 bytes): "b:{userId}:{minutes}", "l:{userId}", "u:{userId}", "r:{requestId}:{minutes}".
/// </summary>
public static class BotUi
{
    public static readonly int[] BonusOptions = [15, 30, 60];

    public static string Format(BotAction action) => action switch
    {
        GrantBonusAction a => $"b:{a.UserId}:{a.Minutes}",
        LockNowAction a => $"l:{a.UserId}",
        UnlockAction a => $"u:{a.UserId}",
        AnswerRequestAction a => $"r:{a.RequestId}:{a.Minutes}",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    public static BotAction? Parse(string? data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        var parts = data.Split(':');
        if (!parts.Skip(1).All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _))) return null;
        var numbers = parts.Skip(1).Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();

        return (parts[0], numbers.Length) switch
        {
            ("b", 2) when numbers[1] > 0 && numbers[1] <= 240 => new GrantBonusAction(numbers[0], numbers[1]),
            ("l", 1) => new LockNowAction(numbers[0]),
            ("u", 1) => new UnlockAction(numbers[0]),
            ("r", 2) when numbers[1] <= 240 => new AnswerRequestAction(numbers[0], numbers[1]),
            _ => null
        };
    }

    /// <summary>+15 / +30 / +60 and Lock now (or Unlock if the parent already locked the user).</summary>
    public static IReadOnlyList<IReadOnlyList<InlineButton>> UserKeyboard(int userId, bool parentLocked) =>
    [
        BonusOptions.Select(m => new InlineButton($"+{m} min", Format(new GrantBonusAction(userId, m)))).ToList(),
        [parentLocked
            ? new InlineButton("Unlock", Format(new UnlockAction(userId)))
            : new InlineButton("Lock now", Format(new LockNowAction(userId)))]
    ];

    public static string FormatStatus(
        string username, LimitConfig limit, UsageRecord? usage, SessionState state, DateTime? lockedOutAt, DateTime now)
    {
        var allowed = UsageRecord.AllowedMinutes(limit, usage);
        var used = usage?.MinutesUsed ?? 0;
        var bonus = usage?.BonusMinutes ?? 0;
        var time = TimeOnly.FromDateTime(now);
        var inWindow = time >= limit.ScheduleStart && time < limit.ScheduleEnd;

        var lines = new List<string> { username };

        var bonusText = bonus > 0 ? $" (incl. {FormatMinutes(bonus)} bonus)" : "";
        var leftText = !inWindow
            ? "outside allowed hours"
            : $"{FormatMinutes(UsageWarnings.GetRemainingMinutes(allowed, used, limit.ScheduleEnd, time))} remaining";
        lines.Add($"Used {FormatMinutes(used)} of {FormatMinutes(allowed)}{bonusText}, {leftText}");
        lines.Add($"Allowed hours today: {limit.ScheduleStart:HH\\:mm}-{limit.ScheduleEnd:HH\\:mm}");

        lines.Add("Session: " + state switch
        {
            SessionState.NotSignedIn => "not signed in",
            SessionState.Active => "active",
            SessionState.ScreenLocked => "screen locked",
            SessionState.SwitchedAway => "switched away / disconnected",
            SessionState.LockedOut when lockedOutAt is { } at =>
                $"locked out since {at:HH\\:mm}, logoff at {at + LockoutPolicy.GracePeriod:HH\\:mm} unless you add time",
            SessionState.LockedOut => "locked out",
            _ => state.ToString()
        });

        if (usage?.ParentLocked == true) lines.Add("Locked by parent for today");
        return string.Join("\n", lines);
    }

    public static string FormatMinutes(int minutes)
    {
        if (minutes < 60) return $"{minutes}min";
        var h = minutes / 60;
        var m = minutes % 60;
        return m > 0 ? $"{h}h {m}min" : $"{h}h";
    }
}

using System.Globalization;

namespace ParentalControl.Core;

public enum ChildMessageKind
{
    /// <summary>The 10- and 1-minute warnings.</summary>
    TimeEndsSoon,
    /// <summary>Signing back in while still out of time; the session is locked again right after.</summary>
    TimeIsOver,
    /// <summary>A parent added time from Telegram.</summary>
    TimeAdded,
    /// <summary>A parent took time away from Telegram.</summary>
    TimeTaken
}

/// <summary>A message to the child: what happened and the number of minutes it is about.</summary>
public sealed record ChildMessage(ChildMessageKind Kind, int Minutes)
{
    public static ChildMessage TimeEndsSoon(int remainingMinutes) => new(ChildMessageKind.TimeEndsSoon, remainingMinutes);
    public static ChildMessage TimeIsOver(TimeSpan logoffAfter) => new(ChildMessageKind.TimeIsOver, (int)logoffAfter.TotalMinutes);
    public static ChildMessage TimeAdded(int minutes) => new(ChildMessageKind.TimeAdded, minutes);
    public static ChildMessage TimeTaken(int minutes) => new(ChildMessageKind.TimeTaken, minutes);
}

/// <summary>
/// Texts of the messages to the child in English, Russian and Polish. The service passes only the kind and
/// the minutes to ParentalControl.Notify, which runs as the child and picks the language of their Windows.
/// Self-contained (no other project types), so ParentalControl.Notify compiles this file in directly.
/// </summary>
public static class ChildMessages
{
    public static readonly string[] Languages = ["en", "ru", "pl"];

    /// <summary>The title and the text in the given language ("ru", "pl"; anything else is English).</summary>
    public static (string Title, string Message) Text(ChildMessage m, string language)
    {
        var n = m.Minutes;
        return language switch
        {
            "ru" => ("Родительский контроль", m.Kind switch
            {
                ChildMessageKind.TimeEndsSoon =>
                    $"Время за компьютером закончится\nчерез {n} {Ru(n)}.\n\n" +
                    "Сохрани свою работу:\nсеанс будет заблокирован.",
                ChildMessageKind.TimeIsOver =>
                    "Время за компьютером закончилось.\n\n" +
                    "Сеанс снова будет заблокирован.\nПрограммы останутся открытыми, пока родители не добавят время; " +
                    $"через {n} {Ru(n)} будет выполнен выход из системы.",
                ChildMessageKind.TimeAdded => $"Родители добавили {n} {Ru(n)}.",
                _ => $"Родители забрали {n} {Ru(n)}."
            }),
            "pl" => ("Kontrola rodzicielska", m.Kind switch
            {
                ChildMessageKind.TimeEndsSoon =>
                    $"Twój czas przy komputerze skończy się\nza {n} {Pl(n)}.\n\n" +
                    "Zapisz swoją pracę:\nsesja zostanie zablokowana.",
                ChildMessageKind.TimeIsOver =>
                    "Twój czas przy komputerze się skończył.\n\n" +
                    "Sesja zostanie ponownie zablokowana.\nProgramy pozostaną otwarte, dopóki rodzic nie doda czasu; " +
                    $"za {n} {Pl(n)} nastąpi wylogowanie.",
                ChildMessageKind.TimeAdded => $"Rodzic dodał {n} {Pl(n)}.",
                _ => $"Rodzic zabrał {n} {Pl(n)}."
            }),
            _ => ("Parental Control", m.Kind switch
            {
                ChildMessageKind.TimeEndsSoon =>
                    $"Your computer time ends\nin {n} {En(n)}.\n\n" +
                    "Save your work:\nthe session will be locked.",
                ChildMessageKind.TimeIsOver =>
                    "Your computer time is over.\n\n" +
                    "The session will be locked again.\nYour programs stay open until a parent adds time; " +
                    $"after {n} {En(n)} you will be logged off.",
                ChildMessageKind.TimeAdded => $"A parent added {n} {En(n)}.",
                _ => $"A parent took away {n} {En(n)}."
            })
        };
    }

    /// <summary>The language of the current user's Windows: "ru", "pl" or "en".</summary>
    public static string CurrentLanguage()
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Contains(language) ? language : "en";
    }

    private static string En(int n) => n == 1 ? "minute" : "minutes";

    // "через 1 минуту / 2 минуты / 5 минут / 21 минуту"
    private static string Ru(int n)
    {
        int mod10 = n % 10, mod100 = n % 100;
        if (mod10 == 1 && mod100 != 11) return "минуту";
        if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14)) return "минуты";
        return "минут";
    }

    // "za 1 minutę / 2 minuty / 5 minut / 22 minuty"
    private static string Pl(int n)
    {
        if (n == 1) return "minutę";
        int mod10 = n % 10, mod100 = n % 100;
        if (mod10 is >= 2 and <= 4 && mod100 is not (>= 12 and <= 14)) return "minuty";
        return "minut";
    }

    /// <summary>Command-line arguments for ParentalControl.Notify: the kind and the minutes, e.g. "TimeEndsSoon 10".</summary>
    public static string ToArguments(ChildMessage m) =>
        $"{m.Kind} {m.Minutes.ToString(CultureInfo.InvariantCulture)}";

    public static bool TryParseArguments(string[] args, out ChildMessage message)
    {
        message = null!;
        if (args.Length != 2
            || !Enum.TryParse<ChildMessageKind>(args[0], out var kind)
            || !Enum.IsDefined(kind)
            || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        message = new ChildMessage(kind, minutes);
        return true;
    }

    /// <summary>Every message with sample numbers, for the preview.</summary>
    public static IReadOnlyList<ChildMessage> AllSamples(TimeSpan logoffAfter) =>
    [
        ChildMessage.TimeEndsSoon(10),
        ChildMessage.TimeEndsSoon(1),
        ChildMessage.TimeIsOver(logoffAfter),
        ChildMessage.TimeAdded(15),
        ChildMessage.TimeTaken(15)
    ];
}

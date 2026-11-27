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
/// Texts of the messages to the child. The service passes only the kind and the minutes to
/// ParentalControl.Notify, which shows the text. Self-contained (no other project types), so
/// ParentalControl.Notify compiles this file in directly.
/// </summary>
public static class ChildMessages
{
    public static (string Title, string Message) Text(ChildMessage m)
    {
        var n = m.Minutes;
        return ("Parental Control", m.Kind switch
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
        });
    }

    private static string En(int n) => n == 1 ? "minute" : "minutes";

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

using System.Diagnostics;
using System.Windows;
using ParentalControl.Core;

namespace ParentalControl.Notify;

/// <summary>
/// Shows a message to the child in a window with large text, on top of other windows, in the language of
/// their Windows. The service starts it in the child's session with the kind of message and the minutes as
/// arguments (see <see cref="ChildMessages.ToArguments"/>). Started without arguments (e.g. double-clicked),
/// it previews every message the child can get, one after another; with "en", "ru" or "pl", in that language.
/// </summary>
public static class Program
{
    // Same as LockoutPolicy.GracePeriod in the service; only used for the preview
    private static readonly TimeSpan LogoffAfter = TimeSpan.FromMinutes(15);

    [STAThread]
    public static int Main(string[] args)
    {
        var language = ChildMessages.CurrentLanguage();
        IReadOnlyList<ChildMessage> messages;
        if (args.Length == 0)
        {
            messages = ChildMessages.AllSamples(LogoffAfter);
        }
        else if (args.Length == 1 && ChildMessages.Languages.Contains(args[0]))
        {
            // Preview in another language: "ParentalControl.Notify.exe pl"
            language = args[0];
            messages = ChildMessages.AllSamples(LogoffAfter);
        }
        else if (ChildMessages.TryParseArguments(args, out var message))
        {
            CloseOlderWindows();
            messages = [message];
        }
        else
        {
            return 1;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var message in messages)
        {
            var (title, text) = ChildMessages.Text(message, language);
            new MessageWindow(title, text).ShowDialog();
        }
        app.Shutdown();
        return 0;
    }

    /// <summary>
    /// Only the latest message stays: e.g. after signing back in while out of time, the notice from the
    /// previous attempt is replaced instead of piling up.
    /// </summary>
    private static void CloseOlderWindows()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            using (other)
            {
                if (other.Id == current.Id || other.SessionId != current.SessionId) continue;
                try
                {
                    other.Kill();
                }
                catch (Exception)
                {
                    // Already gone, or not ours
                }
            }
        }
    }
}

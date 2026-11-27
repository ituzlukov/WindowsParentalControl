using System.Runtime.InteropServices;
using ParentalControl.Core;
using ParentalControl.Core.Platform;

namespace ParentalControl.Service;

/// <summary>
/// Shows a message to the child: in ParentalControl.Notify (large text, on top of other windows, in the
/// language of the child's Windows), started in their session as them; if that fails, in a system message
/// box (WTSSendMessage) in English. Never waits.
/// </summary>
public static class ChildNotifier
{
    // Installed next to the service folder: {app}\service and {app}\notify
    private static readonly string NotifyExe =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "notify", "ParentalControl.Notify.exe"));

    public static bool Show(int sessionId, ChildMessage message, Serilog.ILogger logger)
    {
        if (File.Exists(NotifyExe))
        {
            if (UserProcess.TryStart(sessionId, NotifyExe, ChildMessages.ToArguments(message), out var error))
                return true;
            logger.Warning("Could not start the notification window in session {SessionId} ({Error}); showing a system message",
                sessionId, error);
        }

        var text = ChildMessages.Text(message, "en");
        if (SessionManager.SendMessage(sessionId, text.Title, text.Message)) return true;
        logger.Warning("Failed to show a message in session {SessionId}, Win32 error {Error}",
            sessionId, Marshal.GetLastPInvokeError());
        return false;
    }
}

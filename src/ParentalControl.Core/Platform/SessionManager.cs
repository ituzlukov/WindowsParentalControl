using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ParentalControl.Core.Platform;

public static class SessionManager
{
    public static bool ForceLogoff(int sessionId)
    {
        return NativeMethods.WTSLogoffSession(
            NativeMethods.WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            false);
    }

    public static string? GetSessionUsername(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSUserName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var username = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(username) ? null : username;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionDomain(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSDomainName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var domain = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(domain) ? null : domain;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionUserSid(int sessionId)
    {
        var username = GetSessionUsername(sessionId);
        if (username is null) return null;

        var domain = GetSessionDomain(sessionId);
        var fullName = string.IsNullOrEmpty(domain) ? username : $"{domain}\\{username}";

        try
        {
            var account = new NTAccount(fullName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            return sid.Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    /// <summary>
    /// True if the session is locked (lock screen). Returns false if the lock state is unknown.
    /// </summary>
    public static bool IsSessionLocked(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSSessionInfoEx,
                out var buffer,
                out _))
        {
            return false;
        }

        try
        {
            var info = Marshal.PtrToStructure<NativeMethods.WTSINFOEX_LEVEL1_HEADER>(buffer);
            return info.Level == 1 && info.SessionFlags == NativeMethods.WTS_SESSIONSTATE_LOCK;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    /// <summary>
    /// User sessions that are active or disconnected (e.g. after switching user).
    /// </summary>
    public static List<(int SessionId, string Username, string? Sid, bool IsDisconnected)> GetActiveSessions()
    {
        var sessions = new List<(int, string, string?, bool)>();

        if (!NativeMethods.WTSEnumerateSessionsW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                0,
                1,
                out var pSessionInfo,
                out var count))
        {
            return sessions;
        }

        try
        {
            var structSize = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
            for (int i = 0; i < count; i++)
            {
                var current = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(
                    pSessionInfo + i * structSize);

                var isDisconnected = current.State == NativeMethods.WTS_CONNECTSTATE_CLASS.WTSDisconnected;
                if (current.State != NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive && !isDisconnected)
                    continue;

                var username = GetSessionUsername(current.SessionID);
                if (string.IsNullOrEmpty(username))
                    continue;

                var sid = GetSessionUserSid(current.SessionID);
                sessions.Add((current.SessionID, username, sid, isDisconnected));
            }
        }
        finally
        {
            NativeMethods.WTSFreeMemory(pSessionInfo);
        }

        return sessions;
    }
}

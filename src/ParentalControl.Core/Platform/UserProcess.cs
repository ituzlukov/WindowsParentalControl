using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ParentalControl.Core.Platform;

/// <summary>
/// Starts a process in a user's session, as that user, on their desktop. Only works from a service
/// running as LocalSystem (WTSQueryUserToken needs the TCB privilege).
/// </summary>
public static class UserProcess
{
    public static bool TryStart(int sessionId, string exePath, string arguments, out string? error)
    {
        error = null;
        var userToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var environment = IntPtr.Zero;
        try
        {
            if (!WTSQueryUserToken(sessionId, out userToken))
            {
                error = $"WTSQueryUserToken: {LastError()}";
                return false;
            }

            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out primaryToken))
            {
                error = $"DuplicateTokenEx: {LastError()}";
                return false;
            }

            // The user's own environment; without it the process would inherit the service's
            if (!CreateEnvironmentBlock(out environment, primaryToken, false)) environment = IntPtr.Zero;

            var startupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                lpDesktop = @"winsta0\default"
            };
            var commandLine = new StringBuilder($"\"{exePath}\" {arguments}");

            if (!CreateProcessAsUserW(primaryToken, null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_UNICODE_ENVIRONMENT, environment, Path.GetDirectoryName(exePath),
                    ref startupInfo, out var processInfo))
            {
                error = $"CreateProcessAsUser: {LastError()}";
                return false;
            }

            CloseHandle(processInfo.hProcess);
            CloseHandle(processInfo.hThread);
            return true;
        }
        finally
        {
            if (environment != IntPtr.Zero) DestroyEnvironmentBlock(environment);
            if (primaryToken != IntPtr.Zero) CloseHandle(primaryToken);
            if (userToken != IntPtr.Zero) CloseHandle(userToken);
        }
    }

    private static string LastError() => new Win32Exception(Marshal.GetLastPInvokeError()).Message;

    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(int sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes,
        int impersonationLevel, int tokenType, out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUserW(IntPtr token, string? applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags, IntPtr environment, string? currentDirectory, ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

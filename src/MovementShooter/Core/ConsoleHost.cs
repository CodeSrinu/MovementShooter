using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MovementShooter.Core;

/// <summary>
/// The game is built as a <c>WinExe</c> so it launches without a console window, but a console is
/// extremely useful while an AI assistant is iterating. This helper re-attaches stdout/stderr to the
/// parent terminal so <c>dotnet run</c> shows our logs.
/// </summary>
internal static class ConsoleHost
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static void Attach()
    {
        try
        {
            // Only attach when stdout has not been inherited/redirected. Allocating a console here would
            // steal the redirected handle and silently swallow piped output.
            if (GetStdHandle(StdOutputHandle) != IntPtr.Zero || !AttachConsole(AttachParentProcess))
            {
                return;
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // No console available (for example a double-click launch from Explorer).
        }
        catch (PlatformNotSupportedException)
        {
            // Non-Windows host; nothing to attach to.
        }
    }
}
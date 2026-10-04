using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MovementShooter.Core;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// Tiny dependency-free logger. Writes to the console and to <c>logs/kinetic.log</c> so the
/// output is still inspectable when the game is launched as a windowed <c>WinExe</c>.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);
    public static void Error(string message, Exception exception) =>
        Write(LogLevel.Error, message + Environment.NewLine + exception);

    private static void Write(LogLevel level, string message)
    {
        if (level < MinimumLevel)
        {
            return;
        }

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "[{0:HH:mm:ss.fff}] {1,-5} {2}",
            DateTime.Now,
            level.ToString().ToUpperInvariant(),
            message);

        lock (Gate)
        {
            try
            {
                Console.WriteLine(line);
            }
            catch (IOException)
            {
                // Console went away; the file log below still records the message.
            }

            try
            {
                _file ??= OpenFile();
                _file?.WriteLine(line);
            }
            catch (IOException)
            {
                // Never let logging take the game down.
            }
        }
    }

    private static StreamWriter OpenFile()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "kinetic.log");
        return new StreamWriter(path, append: true, encoding: new UTF8Encoding(false)) { AutoFlush = true };
    }
}

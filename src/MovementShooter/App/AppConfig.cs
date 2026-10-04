using System;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;

namespace MovementShooter.App;

/// <summary>
/// Every tunable value the bootstrap needs lives here so nothing is hard-coded inside systems.
/// Movement/weapon tunables arrive with the gameplay milestones; they get their own config objects.
/// </summary>
public sealed class AppConfig
{
    public int WindowWidth { get; set; } = 1600;
    public int WindowHeight { get; set; } = 900;
    public bool VerticalSync { get; set; } = true;
    public bool FixedTimeStep { get; set; } = true;
    public double TargetFrameRate { get; set; } = 60.0;
    public string WindowTitle { get; set; } = GameName.Value;

    public Color ClearColor { get; set; } = new Color(24, 28, 38);

    public float FieldOfViewDegrees { get; set; } = 75f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 3000f;
    public float MouseSensitivity { get; set; } = 0.0022f;

    /// <summary>Base speed of the bootstrap spectator camera; the player controller has its own values.</summary>
    public float FlyCameraSpeed { get; set; } = 24f;

    /// <summary>Headless-ish render validation: renders N frames into an offscreen target and exits.</summary>
    public bool RunSelfTest { get; set; }

    public int SelfTestWidth { get; set; } = 1280;
    public int SelfTestHeight { get; set; } = 720;
    public string SelfTestOutputDirectory { get; set; } = "artifacts";

    /// <summary>
    /// Rendered-frame budget. Null means "run until the window is closed"; set via <c>--frames</c> to
    /// bound automated runs.
    /// </summary>
    public int? FrameBudget { get; private set; }

    /// <summary>Frames a self-test run renders before it captures and exits.</summary>
    public int SelfTestFrames => FrameBudget ?? DefaultSelfTestFrames;

    private const int DefaultSelfTestFrames = 6;

    public static AppConfig FromArguments(string[] args)
    {
        AppConfig config = new();

        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            switch (argument)
            {
                case "--selftest":
                    config.RunSelfTest = true;
                    break;
                case "--width":
                    config.WindowWidth = ReadInt(args, ref i);
                    break;
                case "--height":
                    config.WindowHeight = ReadInt(args, ref i);
                    break;
                case "--frames":
                    config.FrameBudget = Math.Clamp(ReadInt(args, ref i), 1, 100000);
                    break;
                case "--out":
                    config.SelfTestOutputDirectory = ReadString(args, ref i);
                    break;
                case "--novsync":
                    config.VerticalSync = false;
                    break;
                case "--help":
                case "-h":
                    throw new HelpRequestedException();
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'. Run with --help for usage.");
            }
        }

        config.WindowWidth = Math.Clamp(config.WindowWidth, 640, 7680);
        config.WindowHeight = Math.Clamp(config.WindowHeight, 480, 4320);
        config.SelfTestWidth = Math.Clamp(config.SelfTestWidth, 160, 3840);
        config.SelfTestHeight = Math.Clamp(config.SelfTestHeight, 120, 2160);
        return config;
    }

    public string ResolveOutputDirectory() =>
        Path.IsPathRooted(SelfTestOutputDirectory)
            ? SelfTestOutputDirectory
            : Path.Combine(AppContext.BaseDirectory, SelfTestOutputDirectory);

    public static string Usage =>
        $"{GameName.Value} [options]\n" +
        "  --selftest      Render offscreen, write a PNG screenshot, report pixel stats and exit\n" +
        "  --frames N      Rendered-frame budget for the run (default: unlimited, 6 for --selftest)\n" +
        "  --out DIR       Directory for screenshots (default artifacts)\n" +
        "  --width N       Window width (default 1600)\n" +
        "  --height N      Window height (default 900)\n" +
        "  --novsync       Disable vertical sync\n" +
        "  --help          Show this text";

    private static int ReadInt(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Argument '{args[index]}' expects a value.");
        }

        index++;
        if (!int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"Argument '{args[index - 1]}' expects a whole number, got '{args[index]}'.");
        }

        return value;
    }

    private static string ReadString(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Argument '{args[index]}' expects a value.");
        }

        index++;
        return args[index];
    }

    public sealed class HelpRequestedException : Exception
    {
    }
}
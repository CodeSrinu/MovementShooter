using System;
using MovementShooter.App;
using MovementShooter.Core;

namespace MovementShooter;

/// <summary>Process entry point: parse arguments, start the game loop, translate failures into exit codes.</summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitBadArguments = 2;
    private const int ExitCrash = 3;

    [STAThread]
    private static int Main(string[] args)
    {
        ConsoleHost.Attach();

        AppConfig config;
        try
        {
            config = AppConfig.FromArguments(args);
        }
        catch (AppConfig.HelpRequestedException)
        {
            Console.WriteLine(AppConfig.Usage);
            return ExitSuccess;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(AppConfig.Usage);
            return ExitBadArguments;
        }

        Log.Info($"Starting '{config.WindowTitle}' ({config.WindowWidth}x{config.WindowHeight}).");

        try
        {
            using GameApp app = new(config);
            app.Run();
            return app.ExitCode;
        }
        catch (Exception exception)
        {
            Log.Error("Fatal error during the game loop.", exception);
            return ExitCrash;
        }
    }
}
using System;
using System.Collections.Generic;
using MovementShooter.App;
using MovementShooter.Core;
using MovementShooter.Diagnostics;

namespace MovementShooter;

/// <summary>Process entry point: parse arguments, run headless checks if asked, then the game loop.</summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitChecksFailed = 1;
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

        if (config.RunPhysicsTest)
        {
            return RunPhysicsChecks();
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

    /// <summary>Runs the player and physics checks. Needs no window and no graphics device.</summary>
    private static int RunPhysicsChecks()
    {
        IReadOnlyList<CheckResult> results = PhysicsSelfTest.Run();
        int failures = 0;

        Log.Info("KINETIC player and physics checks");
        foreach (CheckResult result in results)
        {
            string status = result.Passed ? "PASS" : "FAIL";
            Log.Info($"  [{status}] {result.Name} - {result.Detail}");
            if (!result.Passed)
            {
                failures++;
            }
        }

        Log.Info(failures == 0
            ? $"All {results.Count} checks passed."
            : $"{failures} of {results.Count} checks FAILED.");

        return failures == 0 ? ExitSuccess : ExitChecksFailed;
    }
}
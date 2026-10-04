using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MovementShooter.Camera;
using MovementShooter.Core;
using MovementShooter.Diagnostics;
using MovementShooter.Map;

namespace MovementShooter.App;

/// <summary>
/// Composition root and main loop. Everything the game owns is created here, updated in
/// <see cref="Update"/> and drawn in <see cref="Draw"/> - no other class bootstraps the game.
/// </summary>
public sealed class GameApp : Game
{
    private const double MaxDeltaSeconds = 0.1;
    private const int SelfTestFailedExitCode = 1;

    private readonly AppConfig _config;
    private readonly GraphicsDeviceManager _graphics;
    private CameraController? _cameras;
    private World? _world;
    private SelfTestSession? _selfTest;
    private long _frameIndex;

    public GameApp(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = config.WindowWidth,
            PreferredBackBufferHeight = config.WindowHeight,
            PreferredBackBufferFormat = SurfaceFormat.Color,
            PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            GraphicsProfile = GraphicsProfile.Reach,
            SynchronizeWithVerticalRetrace = config.VerticalSync,
        };

        IsFixedTimeStep = config.FixedTimeStep;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / Math.Max(1.0, config.TargetFrameRate));
        IsMouseVisible = true;
        Window.Title = config.WindowTitle;
        Window.AllowUserResizing = true;
    }

    /// <summary>Exit code reported to the shell once the game loop finishes.</summary>
    public int ExitCode { get; private set; }

    protected override void Initialize()
    {
        _cameras = new CameraController(_config);
        Window.Title = $"{_config.WindowTitle} - [{_cameras.Describe()}]";

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _world = new World(GraphicsDevice);
        TestArena.Build(_world);
        Log.Info($"World built with {_world.RenderableCount} renderables.");

        if (_config.RunSelfTest)
        {
            _selfTest = new SelfTestSession(GraphicsDevice, _config);
            Log.Info($"Self-test mode: rendering {_config.SelfTestFrames} frames at {_config.SelfTestWidth}x{_config.SelfTestHeight}.");
        }

        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaSeconds = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, MaxDeltaSeconds);

        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }

        // Mouse look is only accepted while the window has focus, and never during a self-test run.
        _cameras?.Update(deltaSeconds, acceptMouseLook: IsActive && _selfTest is null);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _frameIndex++;

        // With a fixed timestep MonoGame may run several Update calls per Draw, so frames are counted
        // here to keep the frame budget in step with what is actually presented.
        if (!_config.RunSelfTest && _config.FrameBudget.HasValue && _frameIndex >= _config.FrameBudget.Value)
        {
            Log.Info($"Frame budget of {_config.FrameBudget.Value} reached.");
            Exit();
            return;
        }

        GraphicsDevice.SetRenderTarget(_selfTest?.Target);
        if (_selfTest is not null)
        {
            GraphicsDevice.Viewport = new Viewport(0, 0, _selfTest.Target.Width, _selfTest.Target.Height);
        }

        GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, _config.ClearColor, 1f, 0);

        if (_cameras is not null && _world is not null)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            _world.Draw(_cameras.GetViewMatrix(), _cameras.GetProjectionMatrix(viewport.AspectRatio));
        }

        FinishSelfTestIfDone();

        // Present() requires the back buffer to be bound again.
        GraphicsDevice.SetRenderTarget(null);
        base.Draw(gameTime);
    }

    private void FinishSelfTestIfDone()
    {
        if (_selfTest is null || !_selfTest.IsFinished(_frameIndex))
        {
            return;
        }

        RenderReport report = _selfTest.Capture(_frameIndex, out string screenshotPath);
        Log.Info($"Self-test screenshot: {screenshotPath}");
        Log.Info($"Self-test report: {report}");

        bool passed = report.LooksLikeARenderedScene;
        Log.Info(passed ? "Self-test PASSED: 3D scene rendered as expected." : "Self-test FAILED: frame looks empty.");

        ExitCode = passed ? 0 : SelfTestFailedExitCode;
        Exit();
    }

    protected override void UnloadContent()
    {
        _selfTest?.Dispose();
        _selfTest = null;
        _world?.Dispose();
        _world = null;
        base.UnloadContent();
    }
}
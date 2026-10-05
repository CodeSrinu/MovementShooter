using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MovementShooter.Combat;
using MovementShooter.Core;
using MovementShooter.Diagnostics;
using MovementShooter.Map;
using MovementShooter.Physics;
using MovementShooter.Player;
using MovementShooter.Targets;
using MovementShooter.UI;
using MovementShooter.Weapons;

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
    private World? _world;
    private CursorLock? _cursorLock;
    private PhysicsWorld? _physics;
    private PlayerController? _player;
    private PlayerDebugOverlay? _debugOverlay;
    private MovementEffects? _effects;
    private MovementEffectRenderer? _effectRenderer;
    private ExplosionSystem? _explosions;
    private WeaponController? _weapons;
    private CombatTarget? _playerTarget;
    private CombatEffectRenderer? _combatEffects;
    private TestDummyRenderer? _dummyRenderer;
    private List<TestDummy>? _dummies;
    private WeaponInput? _weaponInput;
    private WeaponViewModel? _viewmodel;
    private ViewModelRenderState? _weaponView;
    private FirstPersonWeaponRenderer? _viewmodelRenderer;

    /// <summary>False while the player is dead. Development-only; respawn is a later milestone.</summary>
    private bool _alive = true;

    // Previous movement state, so a cue can be fired on the frame the state *changes*. Reading a per-substep flag
    // from Update would miss a dash entirely whenever the frame ran zero substeps, which is three frames out of
    // four above 144 fps - and would fire it several times over otherwise.
    private MovementState _previousState = MovementState.Airborne;
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

    protected override void LoadContent()
    {
        _world = new World(GraphicsDevice);
        MapRenderer.Build(_world, TestArena.Blocks);
        Log.Info($"World built with {_world.RenderableCount} renderables.");

        _physics = new PhysicsWorld();
        MapColliders.Build(_physics, TestArena.Blocks);
        Log.Info($"Physics world built with {_physics.StaticCount} static colliders.");

        PlayerTuning tuning = _config.Player;
        CursorLock cursorLock = new(
            visible => IsMouseVisible = visible,
            () => new Point(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2),
            () => Mouse.GetState().LeftButton == ButtonState.Pressed);

        _cursorLock = cursorLock;
        _player = PlayerController.Create(_physics, tuning, cursorLock);

        // Combat: one explosion system, shared by every weapon, and one target for the player. The player is
        // registered as their own owner so a rocket they fired is recognised as self-damage when it lands near
        // them - the same comparison any other target would go through.
        _explosions = new ExplosionSystem();
        _playerTarget = new CombatTarget("player", _player.Player.Body, _player.Player.Health, _player.Player);
        _explosions.Register(_playerTarget);

        _weapons = new WeaponController(
            _physics,
            _explosions,
            new RocketLauncher(_config.RocketLauncher),
            _playerTarget);
        _weapons.Projectiles.Register((RocketLauncher)_weapons.Weapon);

        if (_config.ShowPlayerDebug)
        {
            _debugOverlay = new PlayerDebugOverlay(GraphicsDevice, new BitmapFont(GraphicsDevice));
        }

        _effects = new MovementEffects();
        _effectRenderer = new MovementEffectRenderer(GraphicsDevice, _effects);

        _weaponInput = new WeaponInput();
        _combatEffects = new CombatEffectRenderer(GraphicsDevice, _weapons.Projectiles, _explosions);

        // The first-person launcher. Presentation only: it reads the camera's basis and the weapon's recoil state,
        // and writes neither back. Nothing here can affect movement.
        ViewModelTuning viewTuning = _config.WeaponViewModel;
        _viewmodel = new WeaponViewModel(viewTuning);
        _weaponView = new ViewModelRenderState(_viewmodel, viewTuning);
        _viewmodelRenderer = new FirstPersonWeaponRenderer(GraphicsDevice, _viewmodel, viewTuning);

        // A row of dummies south of the spawn, so knockback and falloff can be compared side by side by walking
        // along the line. Not enemies: no behaviour, only health and a body to throw.
        _dummies = new List<TestDummy>();
        _dummyRenderer = new TestDummyRenderer(GraphicsDevice);

        for (int i = 0; i < DummyCount; i++)
        {
            TestDummy dummy = new(_physics, $"dummy{i + 1}", DummyPosition(i));
            _dummies.Add(dummy);
            _explosions.Register(dummy.Target);
        }

        if (_config.RunSelfTest)
        {
            _selfTest = new SelfTestSession(GraphicsDevice, _config);
            Log.Info($"Self-test mode: rendering {_config.SelfTestFrames} frames at {_config.SelfTestWidth}x{_config.SelfTestHeight}.");
        }

        // Gameplay owns the cursor: capture it as soon as the window is ready.
        cursorLock.SetLocked(true);
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaSeconds = (float)Math.Min(gameTime.ElapsedGameTime.TotalSeconds, MaxDeltaSeconds);

        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            _player?.Player.CursorLock?.SetLocked(false);
        }

        if (_player is not null)
        {
            _player.Update(deltaSeconds);
            UpdateMovementFeedback(_player, deltaSeconds);
            UpdateWeapon(deltaSeconds);

            // The recoil clock advances with the frame, independently of the fixed substeps. It is pure
            // presentation time, so it has no business running at the simulation rate.
            _viewmodel?.Update(deltaSeconds);
        }

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

        if (_player is not null)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            Matrix view = _player.Player.Camera.GetViewMatrix();
            Matrix projection = _player.Player.Camera.GetProjectionMatrix(viewport.AspectRatio);

            _world?.Draw(view, projection);

            _dummyRenderer?.Draw(_dummies!, view, projection);
            _combatEffects?.Draw(view, projection);
            _effectRenderer?.Draw(view, projection);

            // The held weapon is a separate pass, after the depth buffer is cleared.
            //
            // This is what stops the launcher being clipped by walls when the player stands against one. The model
            // is held at a position that is inside the player's own collision, so with the world's depth still
            // present it would be half-swallowed by any surface the player is near. Clearing depth costs the weapon
            // nothing, and the alternative - giving the weapon real collision - is not wanted yet.
            DrawViewModel(viewport);
        }

        if (_player is not null && _debugOverlay is not null)
        {
            _debugOverlay.Draw(
                _player.Player,
                1f / (float)Math.Max(gameTime.ElapsedGameTime.TotalSeconds, 0.0001),
                _player.LastSubStepCount,
                _weapons,
                _explosions);
        }

        FinishSelfTestIfDone();

        // Present() requires the back buffer to be bound again.
        GraphicsDevice.SetRenderTarget(null);
        base.Draw(gameTime);
    }

    /// <summary>
    /// Ages the movement effects and fires their cues.
    ///
    /// This runs after the player has been advanced and only ever *reads* from it. Nothing here writes to the
    /// player, the camera or the physics world, so the effects cannot influence the simulation - they are a
    /// report on what the movement code already did.
    ///
    /// Cues are triggered by comparing the movement's own running totals and state against the previous frame,
    /// rather than by watching the per-substep flags. A frame that runs no fixed substep - three frames out of
    /// four at 144 fps and above - never sets those flags at all, so a dash would go unmarked exactly when the
    /// frame rate is highest. The totals do not move that way, so a cue cannot be missed or doubled up.
    /// </summary>
    private void UpdateMovementFeedback(PlayerController controller, float deltaSeconds)
    {
        if (_effects is null)
        {
            return;
        }

        _effects.Update(deltaSeconds);
        CheckPlayerDeath();

        PlayerMovement movement = controller.Player.Movement;
        MovementState state = movement.State;

        if (movement.TotalDashes > _lastDashes)
        {
            Vector3 eye = controller.Player.Camera.Position;
            Vector3 direction = movement.IsDashing ? movement.DashDirection : Horizontal(controller.Player.Velocity);
            _effects.SpawnDashStreaks(eye, direction);
        }

        // Dust is tied to the slide *becoming* a slide, not to the key going down: TryStartSlide can decline,
        // and a cue that fired anyway would announce a slide that never happened.
        if (state == MovementState.Sliding && _previousState != MovementState.Sliding)
        {
            float feet = controller.Player.Position.Y - (movement.CurrentHeight * 0.5f);
            _effects.SpawnSlideDust(new Vector3(controller.Player.Position.X, feet, controller.Player.Position.Z), Horizontal(controller.Player.Velocity));
        }

        _lastDashes = movement.TotalDashes;
        _previousState = state;
    }

    /// <summary>Dashes seen last frame, so a dash is detected once however the frame was divided.</summary>
    private int _lastDashes;

    private const int DummyCount = 4;

    /// <summary>
    /// Dummies in a row, off to the player's left and slightly ahead.
    ///
    /// The spawn looks along -Z, so anything at negative Z is straight ahead - but the arena's central ramp rises
    /// right there and hid them. These sit clear of it on open ground, within about 12 m, so the health bars are
    /// legible and one shot can be aimed between two of them to compare falloff by eye.
    /// </summary>
    private static Vector3 DummyPosition(int index) => new(-8f - (index * 5f), 1.2f, 27f);

    private static Vector3 Horizontal(Vector3 velocity) => new(velocity.X, 0f, velocity.Z);

    /// <summary>
    /// Handles firing.
    ///
    /// Firing is edge-and-hold: the button state is sampled every frame and the weapon's own cooldown paces it,
    /// so holding the mouse fires at the fire interval rather than at the frame rate. There is deliberately no
    /// separate "is held" flag here - the cooldown is the rate limit, and a second limiter on top of it would only
    /// make the two disagree.
    /// </summary>
    private void UpdateWeapon(float deltaSeconds)
    {
        if (_weapons is null || _weaponInput is null || _player is null)
        {
            return;
        }

        _weapons.StepCooldowns(deltaSeconds);

        // Fire is only read while the cursor is captured. That is what stops the click that *captures* the cursor
        // from also firing, and it is checked in the input reader too - this is the second of the two guards.
        bool locked = _player.Player.IsCursorLocked;
        WeaponInputState input = _weaponInput.Poll(locked);

        if (!input.FirePressed || _weapons.LiveProjectiles >= MaxLiveRockets)
        {
            return;
        }

        Vector3 eye = _player.Player.Camera.Position;
        Vector3 forward = _player.Player.Camera.Forward;

        // Only a *successful* shot produces feedback. A refused one - still on cooldown - must not flash or
        // recoil, or holding the mouse would read as a machine gun with no rate limit.
        if (!_weapons.TryFire(eye, forward))
        {
            return;
        }

        // A brief view punch. It uses the firing kick rather than the dash kick, and the difference matters: the
        // dash key is edge-triggered so KickForDash fires once per press, but the mouse button can be *held*, so
        // anything the fire path touches has to be safe to trigger every frame.
        _player.Player.Camera.KickForFiring();

        // The muzzle flash and the recoil animation live on the viewmodel, not on the camera.
        _viewmodel?.OnFired();
    }

    /// <summary>
    /// A ceiling on live rockets. Not a design limit so much as a guard: this milestone has no ammo, and a held
    /// mouse button at 0.75 s intervals would otherwise leave an unbounded number in flight and unbounded damage
    /// on the floor.
    /// </summary>
    private const int MaxLiveRockets = 6;

    /// <summary>
    /// Development death handling. Health reaching zero stops the player acting and says so; there is no death
    /// screen or respawn yet, so the player simply cannot act until the game is restarted.
    /// </summary>
    private void CheckPlayerDeath()
    {
        if (_alive || _player is null)
        {
            return;
        }

        _alive = _player.Player.Health.CurrentHealth > 0f;

        if (!_alive)
        {
            Log.Info($"Player down at {LogPos(_player.Player.Position)}.");
        }
    }

    private static string LogPos(Vector3 position) =>
        $"({position.X:0.#}, {position.Y:0.#}, {position.Z:0.#})";

    /// <summary>
    /// Draws the first-person weapon, in its own pass.
    ///
    /// The depth buffer is cleared first, then the weapon is drawn with a projection of its own: a narrower field
    /// of view, and a near plane measured in centimetres. Both are needed - the world's 90 degree field of view
    /// distorts a held object badly, and the world's near plane would clip a model held half a metre away.
    ///
    /// Clearing depth is what stops the launcher being clipped by walls when the player stands against one. The
    /// model is held at a position that is inside the player's own collision, so with the world's depth still
    /// present it would be half-swallowed by any surface the player is near. The alternative - giving the weapon
    /// real collision - is not wanted in this milestone.
    /// </summary>
    private void DrawViewModel(Viewport viewport)
    {
        if (_weaponView is null || _viewmodelRenderer is null || _player is null)
        {
            return;
        }

        GraphicsDevice.Clear(ClearOptions.DepthBuffer, _config.ClearColor, 1f, 0);

        _weaponView.View = _player.Player.Camera.GetViewModelViewMatrix();
        _weaponView.Projection = PlayerCamera.GetViewModelProjectionMatrix(
            viewport.AspectRatio,
            _weaponView.Tuning.ViewModelFieldOfViewDegrees,
            _weaponView.Tuning.ViewModelNearPlane,
            _weaponView.Tuning.ViewModelFarPlane);

        _viewmodelRenderer.Draw(_weaponView.GetWorldMatrix(), _weaponView.View, _weaponView.Projection);
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

    /// <summary>Losing focus must hand the cursor back, or it stays trapped in an unfocused window.</summary>
    protected override void OnDeactivated(object sender, EventArgs e)
    {
        _cursorLock?.HandleWindowDeactivated();
        base.OnDeactivated(sender, e);
    }

    /// <summary>Regaining focus recaptures the cursor, but only if it was held before focus was lost.</summary>
    protected override void OnActivated(object sender, EventArgs e)
    {
        _cursorLock?.HandleWindowActivated();
        base.OnActivated(sender, e);
    }

    protected override void UnloadContent()
    {
        _selfTest?.Dispose();
        _selfTest = null;
        _combatEffects?.Dispose();
        _combatEffects = null;
        _viewmodelRenderer?.Dispose();
        _viewmodelRenderer = null;
        _dummyRenderer?.Dispose();
        _dummyRenderer = null;

        if (_dummies is not null)
        {
            foreach (TestDummy dummy in _dummies)
            {
                dummy.Dispose();
            }

            _dummies.Clear();
            _dummies = null;
        }

        _effectRenderer?.Dispose();
        _effectRenderer = null;
        _effects?.Clear();
        _effects = null;
        _debugOverlay?.Dispose();
        _debugOverlay = null;
        _player?.Dispose();
        _player = null;
        _physics?.Dispose();
        _physics = null;
        _world?.Dispose();
        _world = null;
        base.UnloadContent();
    }
}
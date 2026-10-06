using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MovementShooter.Character;
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
    private CharacterBody? _character;

    /// <summary>The character-carried weapon, resolved from the character's sockets. Presentation only.</summary>
    private WeaponAttachment? _weapon;

    /// <summary>
    /// Set for one frame when a shot actually leaves the barrel, so the character's fire clip
    /// starts on the same frame as the viewmodel recoil rather than a frame later.
    /// </summary>
    private bool _fireAnimationPending;

    /// <summary>Set once the offscreen character preview has been rendered, so the first update can exit.</summary>
    private bool _previewComplete;

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

        // The full-body character. Loaded and posed entirely on this side of the fence: it is handed the player's
        // position and state each frame and never reads physics itself, so it cannot affect movement or collision.
        _character = new CharacterBody(GraphicsDevice, CharacterAsset.Load(), _config.Character);
        Log.Info($"Character loaded: {_character.Asset.VertexCount} verts, {_character.Asset.TriangleCount} tris, " +
                 $"{_character.Asset.BoneCount} bones, {_character.Asset.Clips.Length} clips, " +
                 $"{_character.Asset.Sockets.Length} sockets, " +
                 $"authored height {_character.AuthoredHeight:0.000} m, drawn {_character.ScaledHeight:0.000} m.");

        // The launcher. Attached to the character rather than to the player: it resolves
        // its transform from the character's WeaponSocket every frame, so it follows the
        // animation without any per-clip weapon pose, and a second character would carry
        // its own by handing this a different body.
        _weapon = new WeaponAttachment(GraphicsDevice, WeaponAsset.Load());
        _weapon.Attach(_character);
        Log.Info($"Weapon loaded: {_weapon.Asset.VertexCount} verts, {_weapon.Asset.TriangleCount} tris, " +
                 $"{_weapon.Asset.Submeshes.Length} materials, authored muzzle at z " +
                 $"{_weapon.Asset.Muzzle.Z:0.000} m. Attached to {WeaponAttachment.WeaponSocketName}.");

        if (_config.RunCharacterPreview)
        {
            // Rendered here rather than in Draw because the game is first-person and the
            // character stands at the player's own position: nothing about the normal
            // frame would ever show it. Exit on the first update so the window opens.
            string sheet = CharacterPreview.Render(GraphicsDevice, _character.Asset, _config.Character,
                _config.ResolveOutputDirectory(), _config.Player.CapsuleHeight, _weapon);
            Log.Info($"Character preview written to {Path.Combine(_config.ResolveOutputDirectory(), "character-preview.png")} ({sheet}).");
            _previewComplete = true;
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
        if (_previewComplete)
        {
            Exit();
            return;
        }

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

            UpdateCharacter(deltaSeconds);
        }

        base.Update(gameTime);
    }

    /// <summary>
    /// Reports the player's state to the full-body character and nothing else.
    ///
    /// This is the only place the character meets gameplay, and it is one-way: the character
    /// receives plain facts and returns nothing. Choosing which clip those facts imply, and
    /// the animation timing itself, lives in Character/ - so no clip-selection or blending
    /// logic leaks in here, and the character cannot reach back into movement.
    /// </summary>
    private void UpdateCharacter(float deltaSeconds)
    {
        if (_character is null || _player is null)
        {
            return;
        }

        PlayerController controller = _player;
        PlayerMovement movement = controller.Player.Movement;

        _character.Update(
            deltaSeconds,
            new CharacterAnimationInput(
                IsGrounded: movement.IsGrounded,
                VerticalSpeed: movement.Velocity.Y,
                HorizontalSpeed: movement.HorizontalSpeed,
                IsSliding: movement.State == MovementState.Sliding,
                IsDashing: movement.IsDashing,
                FireTriggered: _fireAnimationPending),
            controller.Player.Position,
            movement.CurrentHeight,
            controller.Player.Camera.Yaw);

        // The weapon reads the character's sockets, so it has to be resolved after the
        // character has posed - otherwise the launcher would be drawn one frame behind
        // the arm it hangs from.
        _weapon?.Update();

        _fireAnimationPending = false;
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

        // Opaque, before the alpha passes, so the character writes depth and the explosion and
        // movement effects sort against it rather than through it.
        _character?.Draw(view, projection);

        // The launcher, in the same pass and after the character, so it depth-tests against
        // the body it hangs from. Its transform comes from the character's WeaponSocket.
        _weapon?.Draw(view, projection);

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

        Vector3 forward = _player.Player.Camera.Forward;

        // The rocket leaves from the character's authored MuzzlePoint socket, not from the
        // camera. It used to start at the camera plus a fixed offset along the aim, which
        // put it in empty space near the player rather than at the end of the barrel, and
        // made it move independently of the weapon.
        //
        // The direction stays the camera's aim: aim is a gameplay decision and must not
        // drift to wherever the animation happens to be pointing the arm. Only the origin
        // is presentation. If the character has no muzzle socket the camera position is
        // used, which is the pre-existing behaviour and still a valid fallback.
        Vector3 origin = _weapon is not null && _weapon.TryGetMuzzle(out Vector3 muzzle)
            ? muzzle
            : _player.Player.Camera.Position;

        // Only a *successful* shot produces feedback. A refused one - still on cooldown - must not flash or
        // recoil, or holding the mouse would read as a machine gun with no rate limit.
        if (!_weapons.TryFire(origin, forward))
        {
            return;
        }

        // A brief view punch. It uses the firing kick rather than the dash kick, and the difference matters: the
        // dash key is edge-triggered so KickForDash fires once per press, but the mouse button can be *held*, so
        // anything the fire path touches has to be safe to trigger every frame.
        _player.Player.Camera.KickForFiring();

        // The muzzle flash and the recoil animation live on the viewmodel, not on the camera.
        _viewmodel?.OnFired();

        // Raise a flag rather than calling the character here: UpdateWeapon runs before
        // UpdateCharacter, and the character must see the shot as one frame of input like
        // every other cue, not as an out-of-band call from the weapon path.
        _fireAnimationPending = true;
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

        _character?.Dispose();
        _character = null;

        // After the character: the weapon holds a reference to it.
        _weapon?.Dispose();
        _weapon = null;

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
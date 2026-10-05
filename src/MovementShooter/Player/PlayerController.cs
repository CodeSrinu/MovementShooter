using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MovementShooter.Player;

/// <summary>
/// Binds devices to the player: reads the keyboard, applies mouse look through <see cref="CursorLock"/>,
/// converts WASD into a world-space direction using the camera yaw, and advances the player.
/// </summary>
/// <remarks>
/// This is the only player class the game loop talks to. It also has a headless mode so the automated
/// physics tests can drive the real player without a window.
/// </remarks>
public sealed class PlayerController : IDisposable
{
    private readonly Player _player;
    private readonly PlayerInput _input = new();
    private readonly Func<PlayerInputState>? _inputOverride;
    private bool _disposed;

    private PlayerController(Player player, Func<PlayerInputState>? inputOverride = null)
    {
        _player = player;
        _inputOverride = inputOverride;
    }

    public Player Player => _player;

    /// <summary>Fixed substeps executed during the most recent <see cref="Update"/>.</summary>
    public int LastSubStepCount { get; private set; }

    /// <summary>Creates a controller for the real game, with mouse capture wired to the window.</summary>
    public static PlayerController Create(
        Physics.PhysicsWorld physics,
        PlayerTuning tuning,
        CursorLock cursorLock)
    {
        ArgumentNullException.ThrowIfNull(cursorLock);
        return new PlayerController(new Player(physics, tuning, cursorLock));
    }

    /// <summary>
    /// Creates a controller with no input devices, for automated tests. <paramref name="inputOverride"/>
    /// lets a test drive the *real* update path - per-frame input sampling and real frame deltas -
    /// without a keyboard, which is the only way to reproduce timing-dependent bugs faithfully.
    /// </summary>
    public static PlayerController CreateHeadless(
        Physics.PhysicsWorld physics,
        PlayerTuning tuning,
        Func<PlayerInputState>? inputOverride = null) =>
        new(new Player(physics, tuning, cursorLock: null), inputOverride);

    /// <summary>Reads input and advances the player by one frame.</summary>
    public void Update(float frameDeltaSeconds)
    {
        CursorLock? cursorLock = _player.CursorLock;
        if (cursorLock is not null)
        {
            cursorLock.Update();
        }

        PlayerInputState state = _inputOverride is not null ? _inputOverride() : _input.Poll();

        if (state.ToggleCursorRequested)
        {
            cursorLock?.Toggle();
        }

        if (cursorLock?.IsLocked == true)
        {
            _player.ApplyLook(cursorLock.ConsumeLookDelta());
        }

        Vector3 wish = _player.Camera.GetWishDirection(state.Move);

        // With no movement input a dash goes where the player is looking. Passing it in keeps the camera basis
        // in the camera, rather than teaching the movement code what a yaw is.
        _player.Movement.SetFallbackDashDirection(_player.Camera.FlatForward);

        Advance(frameDeltaSeconds, wish, state.JumpPressed, state.SlideHeld, state.DashPressed);
    }

    // A frame shorter than one fixed substep runs zero of them, which at 144 fps or above is three frames out
    // of four. A key held for exactly one of those frames would be sampled and then discarded without the
    // movement code ever seeing it, so a tap could vanish entirely at high frame rates. Latching each press
    // until a substep actually consumes it means the input is delivered exactly once, whenever a step runs.
    //
    // All three edge-triggered keys are latched, including the slide. The slide is technically a held key, but
    // `PlayerMovement` detects its own rising edge, so it is just as vulnerable: a one-frame tap landing on a
    // substep-less frame was lost entirely and the slide never began.
    private bool _latchedJump;
    private bool _latchedDash;
    private bool _latchedSlide;

    /// <summary>Advances the player without touching devices.</summary>
    public void StepForTest(
        Vector3 wishDirection,
        bool jumpHeld,
        float frameDeltaSeconds,
        bool slideHeld = false,
        bool dashHeld = false) =>
        Advance(frameDeltaSeconds, wishDirection, jumpHeld, slideHeld, dashHeld);

    private void Advance(float frameDeltaSeconds, Vector3 wishDirection, bool jumpHeld, bool slideHeld, bool dashHeld)
    {
        // Held keys are sampled as-is; only a rising edge needs latching, and only until a step runs.
        _latchedJump |= jumpHeld;
        _latchedDash |= dashHeld;
        _latchedSlide |= slideHeld;

        LastSubStepCount = _player.Advance(
            frameDeltaSeconds,
            wishDirection,
            _latchedJump,
            _latchedSlide,
            _latchedDash);

        // The movement code has now seen the edge, so the latch is spent - but only if a substep actually
        // ran. Clearing it unconditionally would drop the press on exactly the short frames the latch exists
        // to protect.
        if (LastSubStepCount > 0)
        {
            _latchedJump = jumpHeld;
            _latchedDash = dashHeld;
            _latchedSlide = slideHeld;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.Dispose();
    }
}
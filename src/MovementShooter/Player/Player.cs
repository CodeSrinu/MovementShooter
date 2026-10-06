using System;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;
using MovementShooter.Physics;

namespace MovementShooter.Player;

/// <summary>
/// The player entity: a physics body, the character controller that drives it, and a first-person camera.
/// It exposes state and two verbs - apply a look delta, and advance the simulation. Reading devices is the
/// controller's job, which is what lets the automated physics tests drive a real player with no window.
/// </summary>
public sealed class Player : IDisposable
{
    private readonly PhysicsWorld _physics;
    private bool _disposed;

public Player(PhysicsWorld physics, PlayerTuning tuning, CursorLock? cursorLock)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        CursorLock = cursorLock;

        Body = physics.AddDynamicCapsule(tuning.CapsuleRadius, tuning.CapsuleSegmentLength, tuning.SpawnPosition, tuning.Mass);
        Health = new Combat.Health(tuning.MaxHealth);
        Camera = new PlayerCamera(tuning);
        Movement = new PlayerMovement(physics, Body, tuning, Camera);

        Camera.Follow(Movement.Position);
    }

    public PlayerTuning Tuning { get; }

    /// <summary>
    /// Health, so a weapon can damage the player through the ordinary combat path. Owned here because the player
    /// is the thing that gets shot; nothing in the movement code reads it, so a rocket jump cannot accidentally
    /// become a movement tuning value.
    /// </summary>
    public Combat.Health Health { get; }

    /// <summary>True once health has reached zero.</summary>
    public bool IsDead => Health.IsDead;

    /// <summary>Where the eye sits as a fraction of the capsule's height, so crouching lowers the view.</summary>
    private float EyeHeightFraction => Tuning.CapsuleHeight > 0f
        ? Tuning.EyeHeight / Tuning.CapsuleHeight
        : 0.4f;

    /// <summary>Mouse capture, or null when the player is driven without a window (automated tests).</summary>
    public CursorLock? CursorLock { get; }

    public PhysicsBody Body { get; }

    public PlayerMovement Movement { get; }

    public PlayerCamera Camera { get; }

    public Vector3 Position => Movement.Position;

    public Vector3 Velocity => Movement.Velocity;

    public bool IsGrounded => Movement.IsGrounded;

    /// <summary>Speed ignoring vertical motion.</summary>
    public float HorizontalSpeed => Movement.HorizontalSpeed;

    public bool IsCursorLocked => CursorLock?.IsLocked ?? false;

    /// <summary>Applies a mouse delta in pixels.</summary>
    public void ApplyLook(Vector2 mouseDelta) => Camera.ApplyLook(mouseDelta);

    /// <summary>
    /// Advances one rendered frame. Gameplay and physics run in fixed substeps, so the result does not
    /// depend on the frame rate.
    /// </summary>
    /// <param name="frameDeltaSeconds">Length of this frame.</param>
    /// <param name="wishDirection">Desired horizontal direction in world space; need not be normalised.</param>
    /// <param name="jumpHeld">Whether the jump key is currently held.</param>
    /// <param name="slideHeld">Whether the slide key is currently held.</param>
    /// <param name="dashHeld">Whether the dash key is currently held.</param>
    /// <returns>Number of fixed substeps that ran, for diagnostics.</returns>
    public int Advance(float frameDeltaSeconds, Vector3 wishDirection, bool jumpHeld, bool slideHeld = false, bool dashHeld = false)
    {
        int steps = _physics.CalculateSubStepCount(frameDeltaSeconds);
        for (int i = 0; i < steps; i++)
        {
            Movement.Step(PhysicsDefaults.FixedTimeStep, wishDirection, jumpHeld, slideHeld, dashHeld);
            _physics.StepSubStep();
            Movement.AfterStep();
        }

        // A dash only reads as a distinct action if something besides the velocity change says so happened, and
        // the burst is brief enough to be missed at a glance while running. One short field-of-view kick marks it
        // instantly; the slide already announces itself through the lowered stance and gets a smaller, constant
        // widening from the same mechanism.
        //
        // Both cues are *started* here, on the frame the action fires. They are purely visual and are driven
        // from state the movement code already exposes, so nothing here can influence the simulation.
        if (Movement.DashedThisStep)
        {
            Camera.KickForDash();
        }

        Camera.UpdateFeel(frameDeltaSeconds, Movement.State == MovementState.Sliding);

        // The camera rides the player's current stance, so a slide lowers the view with the body instead of
        // leaving the eye hovering at standing height. The eye keeps the same fraction of the capsule it has
        // when standing (EyeHeight / CapsuleHeight), which lands it just under the top of the crouched body
        // rather than at ankle height.
        Camera.Follow(Movement.Position, Movement.CurrentHeight * EyeHeightFraction);
        return steps;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Body.Dispose();
    }
}
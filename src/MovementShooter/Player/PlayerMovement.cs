using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Physics;

namespace MovementShooter.Player;

/// <summary>
/// The character controller: turns a desired direction into velocity, applies gravity and jumps, and
/// answers whether the player is standing on something.
///
/// One <see cref="Step"/> call is one fixed physics substep, so behaviour does not depend on frame rate.
/// Gravity is always applied - never pinned to zero - and the solver is left to stop the body on contact.
/// Pinning was tried and made the capsule creep upwards, so it is deliberately not done.
/// </summary>
public sealed class PlayerMovement
{
    private readonly PhysicsWorld _physics;
    private readonly PhysicsBody _body;
    private readonly PlayerTuning _tuning;

    private float _coyoteRemaining;
    private float _jumpBufferedRemaining;
    private bool _jumpWasHeld;
    private bool _slideWasHeld;
    private bool _isGrounded;
    private bool _wantsToMove;

    private bool _isSliding;
    private bool _slideBlockedFromStanding;

    /// <summary>
    /// The height the stance is currently heading for: slide height while sliding, full height otherwise. The
    /// capsule eases toward this rather than jumping to it, so entering and leaving a slide are both smooth and
    /// the collider always agrees with the camera.
    /// </summary>
    private float _stanceTarget;

    /// <summary>Whether the dash key would currently do anything.</summary>
    private bool _dashAvailable = true;

    /// <summary>Seconds until a ground dash is allowed again.</summary>
    private float _dashCooldownRemaining;

    /// <summary>Dashes spent in the current airborne period, against <see cref="PlayerTuning.AirDashLimit"/>.</summary>
    private int _airDashesUsed;

    private bool _dashWasHeld;

    /// <summary>
    /// Where a dash with no movement input goes: the camera's flattened forward. The camera supplies this, so
    /// the dash is camera-relative without this class needing to know the yaw.
    /// </summary>
    private Vector3 _fallbackDashDirection = Vector3.Forward;

    /// <summary>
    /// Seconds left in the current dash burst, and the direction it is committed to. The burst lasts long
    /// enough to be read as an action rather than a twitch; see <see cref="ApplyDashMovement"/>.
    /// </summary>
    private float _dashTimeRemaining;
    private Vector3 _dashDirection = Vector3.Forward;

    /// <summary>Whether the dash burst is currently live, for the debug readout.</summary>
    public bool IsDashing => _dashTimeRemaining > 0f;

    /// <summary>
    /// The direction the current or most recent dash burst is committed to. Read-only presentation state,
    /// used by the feedback effects so their streaks always agree with the way the player actually travelled.
    /// </summary>
    public Vector3 DashDirection => _dashDirection;

    /// <summary>Seconds left in the current dash burst.</summary>
    public float DashTimeRemaining => _dashTimeRemaining;

    private float _slideTimeRemaining;
    private float _slideElapsed;
    private float _slideCooldownRemaining;

    /// <summary>The direction the slide was launched in, and the axis steering is measured against.</summary>
    private Vector2 _slideDirection;

    /// <summary>The player's current stance height, so probes and the camera agree with the collider.</summary>
    private float _currentHeight;

    public PlayerMovement(PhysicsWorld physics, PhysicsBody body, PlayerTuning tuning)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

        _isGrounded = ProbeWalkableDistanceBelowFeet(out _) <= _tuning.GroundProbeDistance;
        _currentHeight = _tuning.CapsuleHeight;
        _stanceTarget = _tuning.CapsuleHeight;
    }

    public bool IsGrounded => _isGrounded;

    /// <summary>
    /// True while a jump would be allowed: the player is standing on a walkable surface, or left one
    /// within the coyote window. A vertical wall never grants this.
    /// </summary>
    public bool IsSupported => _coyoteRemaining > 0f;

    /// <summary>True on the frame a jump is executed.</summary>
    public bool JumpedThisStep { get; private set; }

    /// <summary>
    /// Running total of jumps executed.
    /// </summary>
    /// <remarks>
    /// <see cref="JumpedThisStep"/> is per fixed substep, so a frame that runs several substeps leaves it
    /// reflecting only the last one - a jump on an earlier substep is invisible to it. This counter does not
    /// reset, so tests and the debug readout can see that a jump happened no matter how the frame was divided.
    /// </remarks>
    public int TotalJumps { get; private set; }

    /// <summary>True on the frame a slide begins.</summary>
    public bool SlideStartedThisStep { get; private set; }

    /// <summary>True on the frame the slide ends, for whatever reason.</summary>
    public bool SlideEndedThisStep { get; private set; }

    /// <summary>The height the stance is easing toward, which is not yet the height it has reached.</summary>
    public float StanceTargetHeight => _stanceTarget;

    /// <summary>
    /// True while the stance is between two heights. A slide begins by aiming at the crouch and easing into
    /// it, so this stays true for a few frames after <see cref="SlideStartedThisStep"/>; feedback that should
    /// feel connected to the low stance waits for it to clear.
    /// </summary>
    public bool IsStanceTransitioning => MathF.Abs(_currentHeight - _stanceTarget) > 0.01f;

    /// <summary>True on the frame a dash is applied.</summary>
    public bool DashedThisStep { get; private set; }

    /// <summary>Running total of dashes applied.</summary>
    public int TotalDashes { get; private set; }

    /// <summary>Current dash availability, for the debug readout.</summary>
    public DashStatus Dash => new(
        _dashAvailable && _dashCooldownRemaining <= 0f,
        _airDashesUsed >= _tuning.AirDashLimit,
        _dashCooldownRemaining,
        _airDashesUsed);

    /// <summary>Which movement behaviour is currently driving the player.</summary>
    public MovementState State { get; private set; } = MovementState.Airborne;

    /// <summary>Seconds left in the current slide, or zero when not sliding.</summary>
    public float SlideTimeRemaining => _slideTimeRemaining;

    /// <summary>How long the player has been sliding, for the debug readout.</summary>
    public float SlideElapsed => _slideElapsed;

    /// <summary>
    /// True while the player is sliding but wants to stand up and cannot yet: something is directly above
    /// them. The stance stays crouched until there is room, rather than forcing the player upright.
    /// </summary>
    public bool SlideBlockedFromStanding => _slideBlockedFromStanding;

    public Vector3 Position => _body.Position;

    public Vector3 Velocity => _body.LinearVelocity;

    /// <summary>Speed ignoring vertical motion.</summary>
    public float HorizontalSpeed => Horizontal(Velocity).Length();

    /// <summary>Seconds of coyote time still available, for the debug display.</summary>
    public float CoyoteTimeRemaining => _coyoteRemaining;

    /// <summary>Seconds a buffered jump will stay valid.</summary>
    public float JumpBufferRemaining => _jumpBufferedRemaining;

    /// <summary>The strict support probe result: how far the feet are from a walkable surface beneath them.</summary>
    public float SupportDistanceBelowFeet => ProbeSupportDistanceBelowFeet(out _);

    /// <summary>
    /// Advances one fixed step.
    /// </summary>
    /// <param name="deltaSeconds">Fixed step length.</param>
    /// <param name="wishDirection">Desired horizontal direction in world space; need not be normalised.</param>
    /// <param name="jumpHeld">Whether the jump key is currently held, used for edge detection.</param>
    public void Step(float deltaSeconds, Vector3 wishDirection, bool jumpHeld) =>
        Step(deltaSeconds, wishDirection, jumpHeld, slideHeld: false, dashHeld: false);

    /// <summary>
    /// Advances one fixed step.
    /// </summary>
    /// <param name="deltaSeconds">Fixed step length.</param>
    /// <param name="wishDirection">Desired horizontal direction in world space; need not be normalised.</param>
    /// <param name="jumpHeld">Whether the jump key is currently held, used for edge detection.</param>
    /// <param name="slideHeld">Whether the slide key is currently held, used for edge detection.</param>
    /// <param name="dashHeld">Whether the dash key is currently held, used for edge detection.</param>
    public void Step(float deltaSeconds, Vector3 wishDirection, bool jumpHeld, bool slideHeld, bool dashHeld = false)
    {
        JumpedThisStep = false;
        SlideStartedThisStep = false;
        SlideEndedThisStep = false;
        DashedThisStep = false;
        _wantsToMove = wishDirection.LengthSquared() > 0f;

        bool jumpPressed = jumpHeld && !_jumpWasHeld;
        _jumpWasHeld = jumpHeld;

        bool slidePressed = slideHeld && !_slideWasHeld;
        _slideWasHeld = slideHeld;

        // Edge detection, so holding the key fires once. A dash that re-triggered every step would be a
        // movement style of its own rather than an action.
        bool dashPressed = dashHeld && !_dashWasHeld;
        _dashWasHeld = dashHeld;

        _dashCooldownRemaining = MathF.Max(0f, _dashCooldownRemaining - deltaSeconds);

        if (jumpPressed)
        {
            _jumpBufferedRemaining = _tuning.JumpBuffer;
        }

        _jumpBufferedRemaining = Math.Max(0f, _jumpBufferedRemaining - deltaSeconds);
        _coyoteRemaining = Math.Max(0f, _coyoteRemaining - deltaSeconds);
        _slideCooldownRemaining = Math.Max(0f, _slideCooldownRemaining - deltaSeconds);

        Vector3 velocity = _body.LinearVelocity;
        Vector3 horizontal = Horizontal(velocity);

        TryStartSlide(horizontal, slidePressed, deltaSeconds);

        // Jump first, so the new vertical velocity is not overwritten below. Coyote time is the only thing that
        // grants a jump, and it is only refreshed while genuinely standing on a walkable surface.
        if (_jumpBufferedRemaining > 0f && _coyoteRemaining > 0f)
        {
            velocity.Y = _tuning.JumpSpeed;
            _jumpBufferedRemaining = 0f;
            _coyoteRemaining = 0f;
            JumpedThisStep = true;
            TotalJumps++;

            // A slide-jump leaves the slide. Horizontal momentum is deliberately untouched: the jump adds
            // vertical velocity only, so the slide's speed carries into the air and ordinary air control
            // takes over from there. Nothing here grants a second jump or bypasses the support rules - the
            // branch above is the same one a standing jump takes.
            if (_isSliding)
            {
                EndSlide();
            }
        }

        // The dash runs after the jump, so `Jump -> Dash` in a single frame fires both and the dash sees the
        // airborne rules rather than the grounded ones. It runs before the movement rule for the same reason:
        // a dash sets a velocity, and letting ground acceleration immediately steer it back toward MoveSpeed
        // would undo the dash on the frame it fired.
        bool dashedThisStep = TryDash(horizontal, wishDirection, dashPressed);
        if (dashedThisStep)
        {
            horizontal = Horizontal(_body.LinearVelocity);
        }

        // A dash is a short *period*, not a single frame. Writing a velocity once and then handing the player
        // straight back to the normal movement rule meant the burst was over almost immediately: at
        // GroundAcceleration the run speed regained control in about four frames, roughly 60 ms. Dashing from
        // a standstill read as a shove, and dashing from a run was indistinguishable from running slightly
        // faster, because the speed that made it distinctive was gone before it could be perceived. Holding
        // the burst for DashDuration is what turns it into a movement action with its own arc.
        _dashTimeRemaining = MathF.Max(0f, _dashTimeRemaining - deltaSeconds);
        bool dashingThisStep = _dashTimeRemaining > 0f;

        // `_isGrounded` is still the value from the previous step, so on the step a jump fires it says the
        // player is on the floor. Applying ground movement then steers the velocity toward MoveSpeed along the
        // wish direction, which quietly destroys momentum at the exact moment a jump is supposed to preserve
        // it - a slide-jump lost a metre per second this way. A jump means the player is leaving the ground, so
        // this step uses the airborne rules.
        bool airborneRules = !_isGrounded || JumpedThisStep || dashedThisStep;

        // While the dash burst is live it owns the horizontal velocity outright, and the ordinary movement rule
        // is skipped for the whole of it. Letting ground acceleration steer back toward MoveSpeed immediately
        // is what made a dash feel like running faster; a dash that cannot be interrupted for its own duration
        // reads as a distinct action the player performed. Normal movement takes over the moment it expires,
        // with no special-case hand-off, so there is no visible reset.
        //
        // The frame the dash fires is excluded: `TryDash` has already written that velocity deliberately, and
        // running the burst on top of it would apply the same blend twice and skip further than intended.
        if (dashingThisStep && !dashedThisStep)
        {
            horizontal = ApplyDashMovement(horizontal, deltaSeconds);
        }
        else if (!dashedThisStep)
        {
            horizontal = _isSliding
                ? ApplySlideMovement(horizontal, wishDirection, deltaSeconds)
                : airborneRules
                    ? ApplyAirMovement(horizontal, wishDirection, deltaSeconds)
                    : ApplyGroundMovement(horizontal, wishDirection, deltaSeconds);
        }

        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        // Gravity is always applied; the contact solver cancels it while resting.
        velocity.Y += _tuning.Gravity * deltaSeconds;

        _body.LinearVelocity = velocity;

        if (_isSliding)
        {
            AdvanceSlideTimer(deltaSeconds);
        }
    }

    /// <summary>
    /// Called after the physics step. Two separate questions are answered here, and keeping them apart is
    /// what stops the player jumping in mid-air:
    /// <list type="bullet">
    /// <item><b>Near the ground?</b> a walkable surface within <see cref="PlayerTuning.GroundProbeDistance"/>
    /// of the feet. Drives friction and the reported grounded state.</item>
    /// <item><b>Standing on something?</b> a walkable surface within
    /// <see cref="PlayerTuning.JumpSupportTolerance"/> of the feet while not moving upwards. Only this
    /// refreshes coyote time, and coyote time is what permits a jump.</item>
    /// </list>
    /// </summary>
    public void AfterStep()
    {
        float groundingDistance = ProbeWalkableDistanceBelowFeet(out _);
        float supportDistance = ProbeSupportDistanceBelowFeet(out float supportNormalY);

        _isGrounded = groundingDistance <= _tuning.GroundProbeDistance;

        // Support is a pure geometry question: a walkable surface directly under the capsule, close enough to
        // be standing on. Nothing else may gate it.
        //
        // An earlier version also required "not ascending", read off vertical speed. That looked like a
        // guard against re-arming coyote during a jump, but running *up* a ramp is also upward motion, so on
        // any incline the player was treated as mid-jump while plainly standing on the slope: coyote never
        // armed and W+Space silently did nothing, while S/A/D kept working. The guard was also unnecessary.
        // A jump lifts the capsule 8.5 / 60 = 0.14 m in the first substep, far outside the 0.05 m support
        // tolerance, so the tolerance alone already refuses to re-arm support mid-jump. Directional
        // velocity has no bearing on whether a jump is allowed.
        bool supported = _isGrounded && supportDistance <= _tuning.JumpSupportTolerance;
        if (supported)
        {
            _coyoteRemaining = _tuning.CoyoteTime;
        }

        // The stance is resolved after the step, once the solver has moved the body, so the headroom test sees
        // where the player actually ended up rather than where they started, and so a reshape never lands in
        // the middle of a step the solver is about to run.
        UpdateStance(PhysicsDefaults.FixedTimeStep);

        // A slide needs ground to exist on. Leaving the ground ends it, which is what makes a slide-jump a
        // jump out of the slide rather than a slide that keeps going in the air.
        if (_isSliding && !_isGrounded)
        {
            EndSlide();
        }

        State = _isSliding
            ? MovementState.Sliding
            : _isGrounded
                ? MovementState.Grounded
                : MovementState.Airborne;

        // Ground and air availability are governed by different things, so they are resolved separately here,
        // where the grounded state is finally known.
        //
        // On the ground the dash is simply on cooldown, so touching down re-arms it at once. In the air it is
        // locked out once the air dash is spent, and stays locked out until the player lands - which is why
        // `W + Space + Shift` on the ground does not consume the air dash: the dash is counted as a ground
        // dash, and landing has not happened.
        if (_isGrounded)
        {
            _airDashesUsed = 0;
            _dashAvailable = true;
        }
        else if (_airDashesUsed < _tuning.AirDashLimit)
        {
            _dashAvailable = true;
        }

        if (_isGrounded && !_wantsToMove && !_isSliding)
        {
            Vector3 velocity = _body.LinearVelocity;
            Vector3 horizontal = Horizontal(velocity);
            float speed = horizontal.Length();
            if (speed > 0f)
            {
                float reduced = Math.Max(0f, speed - (_tuning.GroundFriction * PhysicsDefaults.FixedTimeStep));
                velocity.X = horizontal.X * (reduced / speed);
                velocity.Z = horizontal.Z * (reduced / speed);
                _body.LinearVelocity = velocity;
            }
        }
    }

    /// <summary>
    /// Starts a slide if the player qualifies. Requires being grounded, already moving fast, and having
    /// pressed the key fresh; a held key does not re-enter, which keeps this a technique the player performs
    /// rather than a crouch they hold.
    /// </summary>
    /// <summary>
    /// Holds the dash burst for its remaining duration.
    ///
    /// The burst is not frozen at the value it started with. It holds <see cref="PlayerTuning.DashSpeed"/>
    /// along the dash direction, which is the whole point of the dash: a dash from a run has to end up clearly
    /// faster than the run, not at the run speed the player already had. Speed that is already *above* the dash
    /// speed is bled off toward it rather than snapped down, so dashing into existing speed eases off instead of
    /// cutting, and the hard ceiling stays <see cref="PlayerTuning.DashSpeed"/>.
    ///
    /// Steering is deliberately not honoured here. A dash is a committed direction for its duration; letting the
    /// player curve it turn the dash into a second, slower movement mode instead of a burst. The steering input
    /// is picked up again the instant the burst expires.
    ///
    /// Gravity is untouched, so an air dash keeps falling at exactly the rate it was falling at and a jump is
    /// never cancelled by dashing.
    /// </summary>
    private Vector3 ApplyDashMovement(Vector3 horizontal, float deltaSeconds)
    {
        Vector3 target = _dashDirection * _tuning.DashSpeed;

        // Ease toward the target rather than snapping to it: a dash that snapped would visibly discard
        // momentum the player had earned, which is the opposite of how a burst should read.
        float blend = Math.Clamp(_tuning.DashTurnRate * deltaSeconds, 0f, 1f);
        Vector3 result = Vector3.Lerp(horizontal, target, blend);

        float speed = result.Length();
        if (speed > _tuning.DashSpeed)
        {
            result *= _tuning.DashSpeed / speed;
        }

        return result;
    }

    /// <summary>
    /// Fires a dash if the player qualifies, returning true when it did.
    ///
    /// This only changes the velocity handed to the solver. Nothing moves the body, so a dash into a wall is
    /// stopped by the same contact resolution as running into one: no tunnelling, no penetration, no wall jump.
    /// Vertical velocity is never touched, so an air dash mid-fall keeps falling at the same rate and a dash
    /// during a jump does not cancel it.
    /// </summary>
    private bool TryDash(Vector3 horizontal, Vector3 wishDirection, bool dashPressed)
    {
        if (!dashPressed)
        {
            return false;
        }

        if (!_dashAvailable || _dashCooldownRemaining > 0f)
        {
            return false;
        }

        // Dashing out of a slide is disabled for this milestone: a slide is committed to its momentum, and a
        // dash would either cancel it or stack speed with it. Both are ambiguous until the two are designed
        // together, so the dash is simply unavailable rather than half-working.
        if (_isSliding && !_tuning.DashDuringSlide)
        {
            return false;
        }

        // Movement input decides the direction; with no input the dash goes where the player is looking. The
        // caller supplies the wish direction already rotated into world space by the camera, so this is correct
        // at any yaw without knowing anything about yaw here.
        Vector3 direction = wishDirection.LengthSquared() > 0f
            ? new Vector3(wishDirection.X, 0f, wishDirection.Z)
            : _fallbackDashDirection;

        direction.Y = 0f;
        if (direction.LengthSquared() < 0.0001f)
        {
            return false;
        }

        direction = Vector3.Normalize(direction);

        // Keep most of the existing momentum and add a burst along the dash direction, then cap the total at
        // DashSpeed. The cap is what stops dash-on-dash from compounding: momentum carries, but never past a
        // fixed ceiling, so the fastest a dash can ever make the player is the same number every time.
        // Start the burst partway toward the target rather than all the way there. Jumping straight to the dash
        // speed meant a dash from a run-up looked identical to a dash from standing - both simply arrived at
        // DashSpeed - and, because that speed was then immediately surrendered to ground acceleration, neither
        // read as a burst at all. Applying DashMomentumPreservation of the difference keeps the player's
        // existing momentum in the transition so the acceleration into the dash is visible, while the burst
        // itself still ends at exactly DashSpeed.
        Vector3 target = direction * _tuning.DashSpeed;
        float preserved = Math.Clamp(_tuning.DashMomentumPreservation, 0f, 1f);
        Vector3 result = horizontal + ((target - horizontal) * preserved);

        float length = result.Length();
        if (length > _tuning.DashSpeed)
        {
            result *= _tuning.DashSpeed / length;
        }

        Vector3 velocity = _body.LinearVelocity;
        velocity.X = result.X;
        velocity.Z = result.Z;
        _body.LinearVelocity = velocity;

        _dashDirection = direction;
        _dashTimeRemaining = _tuning.DashDuration;

        _dashCooldownRemaining = _tuning.DashCooldown;
        DashedThisStep = true;
        TotalDashes++;

        if (!_isGrounded)
        {
            _airDashesUsed++;
        }

        _dashAvailable = false;

        return true;
    }


    private void TryStartSlide(Vector3 horizontal, bool slidePressed, float deltaSeconds)
    {
        if (_isSliding || !slidePressed || !_isGrounded || _slideCooldownRemaining > 0f)
        {
            return;
        }

        float speed = horizontal.Length();
        if (speed < _tuning.SlideMinimumSpeed)
        {
            return;
        }

        // The slide inherits the player's existing momentum. It is clamped rather than boosted: the entry
        // speed comes from how fast the player was already running, and a player who somehow arrives faster
        // than the range allows is trimmed instead of launched.
        (float minimum, float maximum) = _tuning.SlideSpeedRange;
        float entrySpeed = Math.Clamp(speed, minimum, maximum);

        _slideDirection = new Vector2(horizontal.X / speed, horizontal.Z / speed);
        horizontal = new Vector3(_slideDirection.X * entrySpeed, 0f, _slideDirection.Y * entrySpeed);

        _isSliding = true;
        _slideTimeRemaining = _tuning.SlideDuration;
        _slideElapsed = 0f;
        SlideStartedThisStep = true;

        // The crouch itself is deferred to AfterStep, and arrives gradually rather than as a cut. Reshaping
        // mid-step means moving the body down inside a step that is about to be solved, and the solver resolves
        // the resulting pose change as penetration and pushes the player off the floor - the slide read as a
        // small hop. Applying it after the solver has already moved the body leaves the next step starting from
        // a consistent, settled pose.
        //
        // The target height is only *aimed at* here; UpdateStance walks toward it at SlideCrouchDownSpeed. The
        // crouch used to be applied in one reshape on this frame, which dropped the camera instantly while the
        // slide took a moment to build up - the entry read as a cut and the exit, which was already gradual, as
        // a smooth settle. Now both ends of the stance change move at a comparable rate.
        _stanceTarget = _tuning.SlideHeight;

        // Write the clamped entry speed straight back, so the slide starts from it.
        Vector3 velocity = _body.LinearVelocity;
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;
        _body.LinearVelocity = velocity;
    }

    /// <summary>
    /// Slide momentum: the existing velocity carries, steering nudges it toward the input, and friction
    /// bleeds it off. Deliberately additive rather than a target velocity - steering must never brake the
    /// player or scale their speed down to some fixed number, which is what would throw away the momentum
    /// the slide exists to preserve.
    /// </summary>
    private Vector3 ApplySlideMovement(Vector3 horizontal, Vector3 wishDirection, float deltaSeconds)
    {
        float speed = horizontal.Length();
        if (speed <= 0f)
        {
            return horizontal;
        }

        Vector3 direction = horizontal / speed;

        if (wishDirection.LengthSquared() > 0f)
        {
            Vector3 wish = Vector3.Normalize(wishDirection);

            // Steering rotates the slide toward the input; it never adds speed. Adding an acceleration along the
            // wish direction instead would compound every substep, and a slide would quietly accelerate the
            // player past their run speed - momentum gained for free, which is the opposite of the point.
            // Interpolating the direction keeps the magnitude untouched and makes a hard 90 degree change
            // impossible: only SlideSteering's share of the turn happens per step.
            float authority = Math.Clamp(_tuning.SlideSteering * deltaSeconds, 0f, 1f);
            Vector3 steered = Vector3.Normalize(Vector3.Lerp(direction, wish, authority));

            // Reject a blend that flipped past the wish direction, which Lerp can do on an opposed input.
            if (Vector3.Dot(steered, wish) < 0f)
            {
                steered = wish;
            }

            direction = steered;
        }

        _slideDirection = new Vector2(direction.X, direction.Z);

        float reduced = MathF.Max(0f, speed - (_tuning.SlideFriction * deltaSeconds));
        return direction * reduced;
    }

    private void AdvanceSlideTimer(float deltaSeconds)
    {
        _slideTimeRemaining -= deltaSeconds;
        _slideElapsed += deltaSeconds;

        if (_slideTimeRemaining <= 0f)
        {
            EndSlide();
        }
    }

    /// <summary>
    /// Ends the slide. The stance does not snap back here: returning to full height is a separate, gradual
    /// step so a slide that ends under a low ceiling leaves the player crouched instead of inside it.
    /// </summary>
    private void EndSlide()
    {
        if (!_isSliding)
        {
            return;
        }

        _isSliding = false;
        _slideTimeRemaining = 0f;
        _slideCooldownRemaining = _tuning.SlideCooldown;
        SlideEndedThisStep = true;

        // Aim back at full height. The walk up is still UpdateStance's job and still subject to the headroom
        // check, so a slide that ends under a low ceiling leaves the player crouched rather than inside it.
        _stanceTarget = _tuning.CapsuleHeight;
    }

    /// <summary>
    /// Sets the collider to <paramref name="height"/> tall while keeping the feet where they are. Growing a
    /// capsule upward from a fixed foot position is what stops the player from sinking into the floor on the
    /// way down and from popping off it on the way up.
    /// </summary>
    private void ApplyStance(float height, float deltaSeconds)
    {
        height = Math.Clamp(height, _tuning.CapsuleRadius * 2f, _tuning.CapsuleHeight);

        float previous = _currentHeight;
        if (Math.Abs(previous - height) < 0.001f)
        {
            return;
        }

        _currentHeight = height;

        // The capsule's half height is its total height over two. Feet sit half a height below the centre,
        // so holding the feet fixed means lowering the centre as the capsule shrinks.
        float previousHalf = previous * 0.5f;
        float newHalf = height * 0.5f;
        float feetY = _body.Position.Y - previousHalf;

        float segmentLength = Math.Max(0f, height - (_tuning.CapsuleRadius * 2f));
        _physics.ReshapeCapsule(_body.Handle, _tuning.CapsuleRadius, segmentLength);

        // The feet must not move, so the centre does - and the move must keep the velocity, or the crouch
        // would cancel the momentum the slide exists to preserve.
        //
        // Order matters. The bounds have to be recomputed *after* the body has been moved, because the broad
        // phase derives them from the pose it last saw. Recomputing first, then dropping the body 0.4 m to
        // crouch, leaves the solver with a stale tall box centred 0.4 m too high. It resolved that phantom
        // overlap by pushing the player sideways at speed: the slide appeared to accelerate to 14 m/s and the
        // player was shoved off their feet.
        Vector3 position = _body.Position;
        position.Y = feetY + newHalf;
        _body.SetPositionKeepingVelocity(position);
        _physics.RefreshBounds(_body.Handle);
    }

    /// <summary>
    /// Walks the stance toward its target height, gradually, in whichever direction is called for.
    ///
    /// Downward movement (into a slide) is unrestricted - the capsule is shrinking into space the player is
    /// already in, so there is nothing to collide with. Upward movement checks the headroom first and gives up
    /// rather than forcing the collider into a ceiling, which is what keeps a slide that ends under low geometry
    /// from putting the player inside it. Called every step regardless of slide state.
    /// </summary>
    private void UpdateStance(float deltaSeconds)
    {
        // Crouching first, and before any "already standing" shortcut. On the frame a slide begins the capsule
        // is still at full height while the target has already dropped to the crouch, so testing for standing
        // height first would find nothing to do and the crouch would never start.
        if (_currentHeight > _stanceTarget + 0.001f)
        {
            // Crouching. Aims at the slide height at SlideCrouchDownSpeed and stops there exactly, so the
            // stance settles rather than creeping and never overshoots into a shorter capsule than configured.
            float drop = _tuning.SlideCrouchDownSpeed * deltaSeconds;
            float crouchTo = MathF.Max(_stanceTarget, _currentHeight - drop);
            ApplyStance(crouchTo, deltaSeconds);

            _slideBlockedFromStanding = false;
            return;
        }

        // Neither crouching nor rising: already standing. Reaching this with a lower target would mean the
        // target is unreachable from here, so it is corrected rather than left hanging.
        if (_currentHeight >= _tuning.CapsuleHeight - 0.001f)
        {
            _stanceTarget = _tuning.CapsuleHeight;
            _slideBlockedFromStanding = false;
            return;
        }

        // Rising. The target is whatever the crouch left behind - normally full height - and may be lower still
        // if something has since asked for another crouch.
        if (_physics.CapsuleBlocked(StandCentreFor(_stanceTarget), _tuning.CapsuleRadius, _stanceTarget, _body.Collidable))
        {
            // Something is directly overhead. Stay crouched: forcing the player upright would put the
            // collider inside the ceiling.
            _slideBlockedFromStanding = true;
            return;
        }

        float rise = _tuning.SlideStandUpSpeed * deltaSeconds;
        float target = MathF.Min(_stanceTarget, _currentHeight + rise);
        ApplyStance(target, deltaSeconds);

        _slideBlockedFromStanding = _currentHeight < _stanceTarget - 0.001f;
    }

    /// <summary>Where the capsule centre would be for a given height, with the feet left where they are.</summary>
    private Vector3 StandCentreFor(float height)
    {
        Vector3 position = _body.Position;
        position.Y = position.Y - (_currentHeight * 0.5f) + (height * 0.5f);
        return position;
    }

    /// <summary>The player's current stance height in metres, for the camera and the debug readout.</summary>
    public float CurrentHeight => _currentHeight;

    /// <summary>
    /// Sets where a dash with no movement input goes. Supplied by the camera each frame, so the camera basis
    /// stays in the camera and this class never has to know what a yaw is.
    /// </summary>
    public void SetFallbackDashDirection(Vector3 cameraForward)
    {
        cameraForward.Y = 0f;
        _fallbackDashDirection = cameraForward.LengthSquared() > 0.0001f
            ? Vector3.Normalize(cameraForward)
            : Vector3.Forward;
    }

    private Vector3 ApplyGroundMovement(Vector3 horizontal, Vector3 wishDirection, float deltaSeconds)
    {
        if (wishDirection.LengthSquared() <= 0f)
        {
            // No input: friction is applied in AfterStep, once the solver's own push has been applied.
            return horizontal;
        }

        Vector3 wish = Vector3.Normalize(wishDirection);
        Vector3 target = wish * _tuning.MoveSpeed;
        Vector3 difference = target - horizontal;
        float allowedChange = _tuning.GroundAcceleration * deltaSeconds;

        return difference.Length() <= allowedChange
            ? target
            : horizontal + (difference * (allowedChange / difference.Length()));
    }

    private Vector3 ApplyAirMovement(Vector3 horizontal, Vector3 wishDirection, float deltaSeconds)
    {
        if (wishDirection.LengthSquared() <= 0f)
        {
            return horizontal;
        }

        Vector3 wish = Vector3.Normalize(wishDirection);

        // Air control adds speed along the wish direction and never removes any. Accelerating toward a target
        // velocity instead would quietly brake a player who is already moving faster than the target, which is
        // exactly the momentum this game is about.
        float headroom = _tuning.MoveSpeed - Vector3.Dot(horizontal, wish);
        if (headroom > 0f)
        {
            horizontal += wish * Math.Min(_tuning.AirAcceleration * deltaSeconds, headroom);
        }

        return horizontal;
    }

    /// <summary>
    /// Distance from the capsule's feet down to the closest walkable surface, or positive infinity when
    /// there is none. "Walkable" is decided by the existing surface-normal rule, so a vertical wall
    /// (normal Y of about 0) is never returned: walls can never act as ground.
    ///
    /// This is the <b>loose</b> question - "is anything walkable near the feet?" - and it uses every probe
    /// offset so that standing on the lip of a platform does not flicker. It answers <see cref="IsGrounded"/>
    /// only. Jump support is answered by <see cref="ProbeSupportDistanceBelowFeet"/>, which is stricter.
    /// </summary>
    private float ProbeWalkableDistanceBelowFeet(out float normalY) =>
        Probe(includeOffsetProbes: true, measurePerpendicular: true, out normalY);

    /// <summary>
    /// Distance from the capsule's feet down to the closest walkable surface that is actually underneath
    /// the capsule, or positive infinity when there is none.
    ///
    /// This is the <b>strict</b> question - "is the capsule resting on something?" - and it is the only one
    /// that may arm a jump. Only probes within the capsule's own radius qualify. The wider offset probes
    /// reach up to 0.7 m, which is well outside the 0.4 m capsule, so a player standing in mid-air beside a
    /// ledge would have a probe hanging over that ledge's top face; because the rays start at the capsule
    /// centre, such a hit reads as a surface <i>below</i> the feet and satisfied every support tolerance.
    /// Jump support was therefore re-armed in mid-air and repeated Space presses walked the player up the
    /// wall one apex at a time. A wall only fails to be ground if nothing underneath the capsule is walkable.
    /// </summary>
    private float ProbeSupportDistanceBelowFeet(out float normalY) =>
        Probe(includeOffsetProbes: false, measurePerpendicular: true, out normalY);

    private float Probe(bool includeOffsetProbes, bool measurePerpendicular, out float normalY)
    {
        Vector3 centre = _body.Position;

        // The reach follows the current stance, not the standing capsule. While sliding, the feet are closer
        // to the centre, so using the standing half height would leave the probe short by the difference and
        // start reporting a grounded player as airborne mid-slide.
        float maxDistance = (_currentHeight * 0.5f) + _tuning.GroundProbeDistance;
        CollidableId ignore = _body.Collidable;
        float closest = float.PositiveInfinity;
        normalY = float.NaN;

        // The capsule never rotates, so the probe offsets can be used in world space directly.
        foreach (Vector3 offset in _tuning.GroundProbeOffsets)
        {
            bool withinCapsule = new Vector2(offset.X, offset.Z).Length() <= _tuning.CapsuleRadius;

            if (!includeOffsetProbes && !withinCapsule)
            {
                continue;
            }

            RayHit hit = _physics.Raycast(centre + offset, Vector3.Down, maxDistance, ignore);

            // A ray pointing down can only reach something at a non-negative distance. A probe origin that
            // starts inside a collider can otherwise report an intersection "above" the feet.
            if (!hit.Hit || hit.Distance < 0f || hit.Normal.Y < _tuning.MinimumGroundNormalY)
            {
                continue;
            }

            // How far the capsule actually sits from the surface it is resting on. A downward ray on a
            // slope travels further than the true gap between the capsule and the plane, because the ray
            // is vertical while the surface is not: the gap is the ray length projected onto the surface
            // normal. Without this, standing on a ramp reads as being tens of centimetres above it, which
            // ate the whole support tolerance and made W+Space silently do nothing on any incline.
            float distance = measurePerpendicular
                ? hit.Distance * hit.Normal.Y
                : hit.Distance;

            if (distance < closest)
            {
                closest = distance;
                normalY = hit.Normal.Y;
            }
        }

        return closest - (_currentHeight * 0.5f);
    }

    private static Vector3 Horizontal(Vector3 velocity) => new(velocity.X, 0f, velocity.Z);
}
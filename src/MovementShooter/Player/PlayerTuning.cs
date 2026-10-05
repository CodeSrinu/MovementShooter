using Microsoft.Xna.Framework;

namespace MovementShooter.Player;

/// <summary>
/// Every player tuning value in one place. Units are metres, seconds and metres per second; nothing in the
/// player systems should contain a bare gameplay number.
/// </summary>
public sealed class PlayerTuning
{
    // ------------------------------------------------------------------ body

    /// <summary>Capsule radius in metres.</summary>
    public float CapsuleRadius { get; set; } = 0.4f;

    /// <summary>Capsule segment length (the part between the two cap centres).</summary>
    public float CapsuleSegmentLength { get; set; } = 1.0f;

    /// <summary>Total standing height of the capsule: 2 * radius + segment length.</summary>
    public float CapsuleHeight => (CapsuleRadius * 2f) + CapsuleSegmentLength;

    /// <summary>Half the capsule height, i.e. the distance from the body centre to the feet.</summary>
    public float CapsuleHalfHeight => CapsuleHeight * 0.5f;

    public float Mass { get; set; } = 80f;

    /// <summary>
    /// Starting health. Combat tuning rather than movement tuning, but it lives with the body because the two
    /// are read together by the debug readout - and, more practically, because a weapon's self-damage is
    /// expressed as a fraction of its own damage, which has to be meaningful against this number.
    /// </summary>
    public float MaxHealth { get; set; } = 100f;

    // --------------------------------------------------------------- movement

    /// <summary>Target ground speed in metres per second.</summary>
    public float MoveSpeed { get; set; } = 7.0f;

    /// <summary>How quickly horizontal speed reaches <see cref="MoveSpeed"/> on the ground.</summary>
    public float GroundAcceleration { get; set; } = 90f;

    /// <summary>
    /// Deceleration applied on the ground when there is no movement input, in metres per second squared.
    ///
    /// This is what decides how a released key feels. At 12 the player coasted for the better half of a second
    /// after letting go, which reads as being pushed rather than as stopping - friction well under a quarter of
    /// <see cref="GroundAcceleration"/> means releasing the key decelerates far more slowly than pressing it
    /// accelerates, so the two felt like different systems. At 45 a full-speed run is shed in about 0.16 s, while
    /// reaching that speed takes 0.08 s: the stop is roughly twice as long as the pull away, which is what makes
    /// it read as friction rather than as the movement being switched off.
    ///
    /// Only ever applied when there is no movement input. Acceleration, and therefore momentum on jumps, in
    /// air, out of slides and out of dashes, is untouched by this value.
    /// </summary>
    public float GroundFriction { get; set; } = 45f;

/// <summary>How quickly horizontal speed can change in the air. Lower than ground for weight.</summary>
    public float AirAcceleration { get; set; } = 24f;

    // ----------------------------------------------------------------- jump

    public float Gravity { get; set; } = -22f;

    /// <summary>Upward speed applied by a jump; jump height follows from <c>v^2 / 2g</c>.</summary>
    public float JumpSpeed { get; set; } = 8.5f;

    /// <summary>Grace period after walking off a ledge during which a jump is still allowed.</summary>
    public float CoyoteTime { get; set; } = 0.10f;

    /// <summary>How long a jump press is remembered before landing, so early presses still work.</summary>
    public float JumpBuffer { get; set; } = 0.12f;

    // ------------------------------------------------------------------ slide

    /// <summary>
    /// Horizontal speed a slide needs before it can start, so the slide is something a moving player does
    /// rather than a crouch. Just under <see cref="MoveSpeed"/> so a player at full run speed can slide.
    /// </summary>
    public float SlideMinimumSpeed { get; set; } = 5.0f;

    /// <summary>
    /// Speed range a slide may begin with, taken from the player's existing horizontal momentum. This is not
    /// a boost: the slide inherits whatever the player already had, clamped into this range so a fast entry
    /// cannot launch the player and a marginal one still feels committed.
    /// </summary>
    public (float Minimum, float Maximum) SlideSpeedRange { get; set; } = (5.0f, 12.0f);

    /// <summary>
    /// How strongly a slide follows the player's steering input, as a fraction of the normal ground
    /// acceleration. Low on purpose: the slide stays committed to its momentum with just enough authority to
    /// correct a line, so a hard direction change is not available.
    /// </summary>
    public float SlideSteering { get; set; } = 0.15f;

    /// <summary>Speed lost per second while sliding. Gentle, so the slide carries.</summary>
    public float SlideFriction { get; set; } = 2.5f;

    /// <summary>Longest a single slide can last. A slide usually ends sooner by running out of speed.</summary>
    public float SlideDuration { get; set; } = 1.1f;

    /// <summary>
    /// Seconds after a slide ends before another may start. Stops a held key from chattering the stance,
    /// while staying short enough that the slide can be repeated as a technique.
    /// </summary>
    public float SlideCooldown { get; set; } = 0.25f;

    /// <summary>Crouched standing height during a slide, in metres.</summary>
    public float SlideHeight { get; set; } = 1.0f;

    /// <summary>
    /// How fast the stance returns to full height once there is headroom, in metres per second. Quick
    /// enough to feel responsive, slow enough not to pop.
    /// </summary>
    public float SlideStandUpSpeed { get; set; } = 6.0f;

    /// <summary>
    /// How fast the stance drops into the slide crouch, in metres per second.
    ///
    /// The crouch used to be a single reshape on the frame the slide began, which read as a hard cut: the view
    /// dropped instantly while the slide itself took a moment to build. This makes the way down match the way
    /// up, so entering and leaving a slide are the same kind of motion in opposite directions. At 9 m/s the
    /// 0.8 m from standing to slide height takes about 0.09 s, against 0.13 s to stand back up - quick enough
    /// that the slide still answers the key immediately, and deliberately never instant.
    ///
    /// The collider and the camera both follow this value, since the camera rides the capsule's actual height,
    /// so the player is never seen to float above or sink into their own collider.
    /// </summary>
    public float SlideCrouchDownSpeed { get; set; } = 9.0f;

    // ------------------------------------------------------------------- dash

    /// <summary>
    /// How fast a dash burst drives the player, in metres per second.
    ///
    /// Well above <see cref="MoveSpeed"/> on purpose: this is the number that makes a dash distinguishable from
    /// running. It is also a hard ceiling, so repeated dashes compound no further than one dash does, and
    /// existing speed above it is eased down rather than discarded.
    /// </summary>
    public float DashSpeed { get; set; } = 15f;

    /// <summary>
    /// How long the burst lasts, in seconds.
    ///
    /// Long enough that the burst can be perceived as its own movement - the gap between a dash and ordinary
    /// acceleration. Short enough that it is an impulse rather than a sprint: at <see cref="DashSpeed"/> this
    /// covers a few metres and then ordinary movement resumes immediately.
    /// </summary>
    public float DashDuration { get; set; } = 0.18f;

    /// <summary>
    /// How quickly the burst reaches <see cref="DashSpeed"/>, in units of the remaining difference per second.
    /// Effectively how hard the dash accelerates the player toward its target speed.
    /// </summary>
    public float DashTurnRate { get; set; } = 40f;

    /// <summary>
    /// Seconds between ground dashes. Short enough to be repeatable, long enough that mashing the key is not a
    /// movement style of its own.
    /// </summary>
    public float DashCooldown { get; set; } = 0.6f;

    /// <summary>
    /// How many dashes are allowed in one airborne period. One, deliberately: multiple air dashes compound into
    /// unbounded air control, which is a later-milestone problem.
    /// </summary>
    public int AirDashLimit { get; set; } = 1;

    /// <summary>
    /// How much of the gap between the player's current velocity and <see cref="DashSpeed"/> the dash closes on
    /// the frame it fires, as a fraction. The rest is closed over the following <see cref="DashDuration"/>.
    ///
    /// This is what keeps a dash from a run-up looking identical to a dash from standing: the burst is already
    /// underway when it fires, so the acceleration into it is visible rather than instantaneous. It is not a
    /// stacking multiplier - the burst still ends at exactly <see cref="DashSpeed"/>, which is the ceiling that
    /// stops dash-on-dash from compounding.
    /// </summary>
    public float DashMomentumPreservation { get; set; } = 0.6f;

    /// <summary>
    /// Whether a dash is allowed while sliding. Off for this milestone: a slide is already committed to its
    /// momentum, and dashing out of one would either cancel the slide or stack speed with it. Both are ambiguous
    /// until slide and dash are designed together.
    /// </summary>
    public bool DashDuringSlide { get; set; }

    // -------------------------------------------------------- ground detection

/// <summary>
    /// How far below the capsule a ground ray may reach before the player counts as airborne. This is a
    /// detection tolerance: it keeps the player "grounded" over small seams and while barely leaving the
    /// floor, so friction does not flicker. It deliberately does <b>not</b> grant jump support.
    /// </summary>
    public float GroundProbeDistance { get; set; } = 0.15f;

    /// <summary>
    /// How far below the feet a walkable surface must be for the player to count as standing on it, and
    /// therefore for a jump to be allowed. Much tighter than <see cref="GroundProbeDistance"/>: a surface a
    /// few centimetres below the feet means the player has left the ground.
    /// </summary>
    public float JumpSupportTolerance { get; set; } = 0.05f;

    /// <summary>
    /// Unused. It used to gate jump support on "not ascending", read off vertical speed, which also
    /// reported a player running up a ramp as airborne and silently blocked W+Space on any incline.
    /// <see cref="PlayerTuning.JumpSupportTolerance"/> alone handles leaving the ground.
    /// </summary>
    public float AscendingSpeedThreshold { get; set; } = 0.1f;

    /// <summary>Minimum upward component of a surface normal to count as standable (0.6 is about 53 degrees).</summary>
    public float MinimumGroundNormalY { get; set; } = 0.6f;

    /// <summary>
    /// Ground probes are cast from the capsule centre plus these offsets, so standing on the lip of a
    /// platform does not drop the player into the air.
    /// </summary>
    public Vector3[] GroundProbeOffsets { get; } =
    {
        new(0f, 0f, 0f),
        new(0.7f, 0f, 0f),
        new(-0.7f, 0f, 0f),
        new(0f, 0f, 0.7f),
        new(0f, 0f, -0.7f),
    };

// ----------------------------------------------------------------- camera

    /// <summary>Vertical field of view in degrees.</summary>
    public float FieldOfViewDegrees { get; set; } = 90f;

    public float NearPlane { get; set; } = 0.05f;

    public float FarPlane { get; set; } = 3000f;

    /// <summary>Height of the eye above the capsule centre.</summary>
    public float EyeHeight { get; set; } = 0.72f;

    // ----------------------------------------------------------- feel impulses

    /// <summary>
    /// Extra degrees of field of view added by a dash, and how long it takes to fade back out.
    ///
    /// A dash from a run is fast enough that the burst itself can be hard to read against steady ground
    /// motion, because the player already expects to be moving. A brief widening of the view is the cheapest cue
    /// that reads instantly without moving the camera in any way the player could mistake for aim drift: it
    /// cannot be steered, and it decays back on its own.
    /// </summary>
    public float DashFovKickDegrees { get; set; } = 7f;

    /// <summary>Seconds for a dash's field-of-view kick to decay back to normal.</summary>
    public float DashFovKickSeconds { get; set; } = 0.22f;

    /// <summary>
    /// Extra degrees of field of view while sliding, and how long it takes to fade in and out.
    ///
    /// The lowered stance is the slide's main feedback and already reads clearly. This is a small extra so a
    /// slide started at a glance still registers, sized to be felt rather than noticed.
    /// </summary>
    public float SlideFovKickDegrees { get; set; } = 3f;

    /// <summary>Seconds for the slide's field-of-view widening to fade in and out.</summary>
    public float SlideFovKickSeconds { get; set; } = 0.12f;

    /// <summary>
    /// Extra degrees of field of view added for a moment when a shot is fired, and how long it takes to fade.
    ///
    /// Deliberately smaller than the dash kick: a launcher's report is mostly in the weapon itself, and a large
    /// view change on every shot would fight the mouse look the player is using to aim the next one.
    /// </summary>
    public float FireFovKickDegrees { get; set; } = 2.5f;

    /// <summary>Seconds for the firing field-of-view punch to decay back to normal.</summary>
    public float FireFovKickSeconds { get; set; } = 0.09f;

    /// <summary>Radians of yaw per pixel of mouse movement.</summary>
    public float MouseSensitivity { get; set; } = 0.0022f;

    /// <summary>Maximum pitch in radians, stopping the view from flipping over the poles.</summary>
    public float MaximumPitch { get; set; } = 1.5f;

    // ----------------------------------------------------------------- spawn

/// <summary>Spawn point: above open ground south of the arena, looking north into it.</summary>
    public Vector3 SpawnPosition { get; set; } = new(0f, 3f, 32f);

    /// <summary>Initial view yaw in radians. Pi looks along -Z, into the arena.</summary>
    public float SpawnYaw { get; set; } = MathHelper.Pi;
}
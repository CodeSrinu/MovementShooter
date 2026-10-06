using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Player;

/// <summary>
/// First-person camera. Owns yaw and pitch and derives the view from the player's eye position, so the
/// camera is always exactly where the player is looking.
/// </summary>
public sealed class PlayerCamera
{
    private readonly PlayerTuning _tuning;

    /// <summary>Extra field of view currently contributed by the dash kick, decaying to zero.</summary>
    private float _dashFovKick;

    /// <summary>Extra field of view currently contributed by sliding, easing toward its target.</summary>
    private float _slideFovKick;

    /// <summary>Extra field of view currently contributed by firing a shot, decaying to zero.</summary>
    private float _fireFovKick;

    public PlayerCamera(PlayerTuning tuning)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        Yaw = tuning.SpawnYaw;
        Pitch = 0f;
        _eyeHeight = tuning.EyeHeight;
    }

    /// <summary>Called on the frame a dash fires, to start the field-of-view kick.</summary>
    public void KickForDash()
    {
        // Saturating, never accumulating. This used to add to the existing kick, which was invisible for the
        // dash - the key is edge-triggered, so it fired once per press - but became a hard crash the moment a
        // *held* action called it every frame: 7 degrees per frame is over 400 degrees per second, which pushed the
        // field of view past 180 and made CreatePerspectiveFieldOfView throw. Taking the larger of the two means
        // the kick is at most one kick, however often it is asked for.
        _dashFovKick = MathF.Max(_dashFovKick, _tuning.DashFovKickDegrees);
    }

    /// <summary>Called when a shot is fired, for a brief field-of-view punch.</summary>
    public void KickForFiring() =>
        _fireFovKick = MathF.Max(_fireFovKick, _tuning.FireFovKickDegrees);

    /// <summary>
    /// Advances the visual feedback for one frame. Exponential decay toward zero, so the kick fades out
    /// smoothly and quickly rather than stepping down, and the perceived duration is the same at any frame rate.
    /// </summary>
    /// <param name="deltaSeconds">Length of this frame.</param>
    /// <param name="sliding">Whether the player is sliding right now.</param>
    public void UpdateFeel(float deltaSeconds, bool sliding)
    {
        _dashFovKick = Approach(_dashFovKick, 0f, _tuning.DashFovKickSeconds, deltaSeconds);
        _fireFovKick = Approach(_fireFovKick, 0f, _tuning.FireFovKickSeconds, deltaSeconds);

        float target = sliding ? _tuning.SlideFovKickDegrees : 0f;
        _slideFovKick = Approach(_slideFovKick, target, _tuning.SlideFovKickSeconds, deltaSeconds);
    }

    /// <summary>
    /// Exponential approach to a target, where <paramref name="seconds"/> is roughly how long the remaining
    /// difference takes to become imperceptible. Independent of frame rate, unlike a per-frame constant step.
    /// </summary>
    private static float Approach(float current, float target, float seconds, float deltaSeconds)
    {
        if (seconds <= 0f)
        {
            return target;
        }

        float blend = 1f - MathF.Exp(-deltaSeconds / seconds);
        return current + ((target - current) * blend);
    }

    /// <summary>
    /// The base field of view plus whatever the dash, slide and firing are currently adding to it.
    ///
    /// Clamped to a range the engine will accept. MonoGame's perspective projection throws outright on a field of
    /// view of 0 or more than 180 degrees, and a throw inside the draw loop is a fatal, unrecoverable crash rather
    /// than a dropped frame. Every kick is separately bounded and decays, so reaching that range should be
    /// impossible - but a hard guarantee here costs nothing and turns a class of fatal bug into a wrong-looking
    /// frame.
    /// </summary>
    public float FieldOfViewDegrees => Math.Clamp(
        _tuning.FieldOfViewDegrees + _dashFovKick + _slideFovKick + _fireFovKick,
        MinimumFieldOfViewDegrees,
        MaximumFieldOfViewDegrees);

    /// <summary>Narrowest field of view the projection will accept, in degrees.</summary>
    public const float MinimumFieldOfViewDegrees = 20f;

    /// <summary>Widest field of view the projection will accept, in degrees. Must stay under 180.</summary>
    public const float MaximumFieldOfViewDegrees = 170f;

    /// <summary>Rotation around the Y axis, in radians. Zero looks along -Z.</summary>
    public float Yaw { get; set; }

    /// <summary>Rotation around the X axis, in radians. Positive looks up.</summary>
    public float Pitch { get; private set; }

    public Vector3 Position { get; private set; }

    public Vector3 Forward => new(
        MathF.Cos(Pitch) * MathF.Sin(Yaw),
        MathF.Sin(Pitch),
        MathF.Cos(Pitch) * MathF.Cos(Yaw));

    /// <summary>
    /// Forward, flattened to the XZ plane: the direction WASD moves along. Yaw 0 looks along +Z and yaw pi
    /// looks along -Z.
    /// </summary>
    public Vector3 FlatForward => new(MathF.Sin(Yaw), 0f, MathF.Cos(Yaw));

    /// <summary>
    /// The player's right-hand direction, flattened. For this yaw parametrisation the right-hand vector is
    /// <c>cross(FlatForward, Up)</c> = (-cos yaw, 0, sin yaw). Note that MonoGame's screen-right is
    /// <c>cross(forward, up)</c>, *not* <c>cross(up, forward)</c> - using the latter silently gives the
    /// left-hand vector and turns A/D and mouse look inside out.
    /// </summary>
    public Vector3 FlatRight => new(-MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

    /// <summary>
    /// Converts a WASD/left-stick vector into a world-space direction: X is the right axis (+1 strafe
    /// right), Y is the forward axis (+1 forward). Stays correct at any yaw because it is built from the
    /// camera basis rather than from world axes.
    /// </summary>
    public Vector3 GetWishDirection(Vector2 move)
    {
        if (move == Vector2.Zero)
        {
            return Vector3.Zero;
        }

        Vector3 direction = (FlatForward * move.Y) + (FlatRight * move.X);
        return direction.LengthSquared() > 0f ? Vector3.Normalize(direction) : Vector3.Zero;
    }

    /// <summary>The camera's right axis in world space. Unchanged by pitch, unlike a look-at's roll axis.</summary>
    public Vector3 Right => new(-MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

    /// <summary>
    /// The camera's up axis in world space: the view direction with its vertical component removed, made
    /// perpendicular to <see cref="Forward"/> again, then rolled to match the pitch. Needed by anything drawn in
    /// view space, such as the first-person weapon.
    /// </summary>
    public Vector3 Up
    {
        get
        {
            Vector3 forward = Vector3.Normalize(Forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            return Vector3.Normalize(Vector3.Cross(right, forward));
        }
    }

    /// <summary>
    /// The view matrix for a first-person model: the camera's basis, with the model at the origin rather than at
    /// the camera's world position.
    ///
    /// Using the real view matrix instead would put the weapon tens of metres down the view axis in world terms,
    /// where a 0.05 m near plane would clip it away entirely. The view matrix still comes from the camera, so the
    /// weapon tracks the look direction exactly - it just is not translated by the eye's position.
    /// </summary>
    public Matrix GetViewModelViewMatrix() => new(
        Right.X, Right.Y, Right.Z, 0f,
        Up.X, Up.Y, Up.Z, 0f,
        Forward.X, Forward.Y, Forward.Z, 0f,
        0f, 0f, 0f, 1f);

    /// <summary>
    /// A projection for a first-person model: narrower field of view and a much closer near plane than the world
    /// uses. Both are necessary - a 90 degree field of view puts the held weapon in such strong perspective that it
    /// looks enormous, and the world's near plane is far enough away that a model held centimetres from the eye
    /// would be clipped.
    /// </summary>
    /// <param name="aspectRatio">Viewport aspect ratio.</param>
    /// <param name="fieldOfViewDegrees">Field of view to use.</param>
    /// <param name="nearPlane">Near plane in metres.</param>
    /// <param name="farPlane">Far plane in metres.</param>
    public static Matrix GetViewModelProjectionMatrix(float aspectRatio, float fieldOfViewDegrees, float nearPlane, float farPlane)
    {
        float safeAspect = aspectRatio <= 0f ? 1f : aspectRatio;
        float clampedFov = Math.Clamp(fieldOfViewDegrees, MinimumFieldOfViewDegrees, MaximumFieldOfViewDegrees);

        return Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(clampedFov),
            safeAspect,
            MathF.Max(nearPlane, 0.001f),
            MathF.Max(farPlane, MathF.Max(nearPlane, 0.001f) + 0.001f));
    }

    public Matrix GetViewMatrix() => Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);

    public Matrix GetProjectionMatrix(float aspectRatio)
    {
        float safeAspect = aspectRatio <= 0f ? 1f : aspectRatio;
        return Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(FieldOfViewDegrees),
            safeAspect,
            _tuning.NearPlane,
            _tuning.FarPlane);
    }

    /// <summary>
    /// Applies a mouse delta in pixels. Screen Y grows downwards, and increasing yaw turns the view to the
    /// *left* in this parametrisation, so both axes are negated: mouse right yaws right, mouse up pitches up.
    /// </summary>
    public void ApplyLook(Vector2 mouseDelta)
    {
        Yaw -= mouseDelta.X * _tuning.MouseSensitivity;
        Pitch -= mouseDelta.Y * _tuning.MouseSensitivity;
        Pitch = Core.MathHelpers.Clamp(Pitch, -_tuning.MaximumPitch, _tuning.MaximumPitch);
    }

    /// <summary>Places the eye at <paramref name="playerPosition"/> plus the configured eye height.</summary>
    public void Follow(Vector3 playerPosition) => Follow(playerPosition, _tuning.EyeHeight);

    /// <summary>
    /// Places the eye for a given stance. During a slide the capsule is shorter, so the eye rides lower with
    /// it: the camera follows the player's actual height rather than hovering at standing height over a
    /// crouched body. The transition is lightly smoothed so it reads as a dip rather than a snap, but only
    /// just - enough to remove popping, not enough to make the controls feel like they lag.
    /// </summary>
    public void Follow(Vector3 playerPosition, float eyeHeight)
    {
        // Fast exponential approach: reaches the target in a few frames, so it tracks input closely.
        float blend = 1f - MathF.Exp(-EyeHeightResponsiveness * EyeSmoothingSeconds);
        _eyeHeight += (eyeHeight - _eyeHeight) * blend;

        Position = new Vector3(playerPosition.X, playerPosition.Y + _eyeHeight, playerPosition.Z);
    }

    /// <summary>Seconds of smoothing applied to the eye-height transition.</summary>
    private const float EyeSmoothingSeconds = 0.05f;

    /// <summary>How sharply the eye chases its target; higher is snappier.</summary>
    private const float EyeHeightResponsiveness = 24f;

    private float _eyeHeight;

    public void Dispose()
    {
        // Nothing to dispose for PlayerCamera
    }
}
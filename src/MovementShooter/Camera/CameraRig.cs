using Microsoft.Xna.Framework;

namespace MovementShooter.Camera;

/// <summary>
/// Base class for anything that can produce view/projection matrices. The FPS camera will eventually
/// derive from this (or replace it) so world rendering never needs to know which camera it uses.
/// </summary>
public abstract class CameraRig
{
    /// <summary>Maximum pitch in radians; keeps the view from flipping over the poles.</summary>
    protected const float MaxPitchRadians = 1.53f;

    protected CameraRig(float fieldOfViewDegrees, float nearPlane, float farPlane)
    {
        FieldOfViewDegrees = fieldOfViewDegrees;
        NearPlane = nearPlane;
        FarPlane = farPlane;
    }

    public Vector3 Position { get; protected set; }

    public Vector3 Target { get; protected set; }

    public Vector3 Up { get; protected set; } = Vector3.Up;

    public float FieldOfViewDegrees { get; set; }

    public float NearPlane { get; set; }

    public float FarPlane { get; set; }

    /// <summary>Per-frame speed multiplier (sprint/boost). Reset to 1 when the modifier is released.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Short name of the rig, shown in the window title.</summary>
    public abstract string Name { get; }

    public Vector3 Forward => Vector3.Normalize(Target - Position);

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Up));

    public Matrix GetViewMatrix() => Matrix.CreateLookAt(Position, Target, Up);

    public Matrix GetProjectionMatrix(float aspectRatio)
    {
        float safeAspect = aspectRatio <= 0f ? 1f : aspectRatio;
        return Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(FieldOfViewDegrees),
            safeAspect,
            NearPlane,
            FarPlane);
    }

    /// <summary>Applies a mouse-look delta in radians.</summary>
    public abstract void Look(float deltaYaw, float deltaPitch);

    /// <summary>Moves the camera along <paramref name="direction"/> (already normalised) for this frame.</summary>
    public abstract void Move(Vector3 direction, float deltaSeconds);

    /// <summary>Applies a scroll-wheel delta.</summary>
    public abstract void Zoom(float delta);

    /// <summary>Restores the rig's default viewpoint.</summary>
    public abstract void Reset();
}
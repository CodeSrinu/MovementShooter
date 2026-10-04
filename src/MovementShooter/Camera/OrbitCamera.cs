using System;
using Microsoft.Xna.Framework;
using MovementShooter.Core;

namespace MovementShooter.Camera;

/// <summary>
/// Orbit camera used to inspect a single structure (and later to frame weapon view-models).
/// </summary>
public sealed class OrbitCamera : CameraRig
{
    private float _yaw = -0.9f;
    private float _pitch = -0.55f;

    public OrbitCamera(float fieldOfViewDegrees, float nearPlane, float farPlane)
        : base(fieldOfViewDegrees, nearPlane, farPlane)
    {
        Reset();
    }

    public override string Name => "Orbit (inspect)";

    public Vector3 Pivot { get; private set; }

    public float Distance { get; private set; }

    public float MinDistance { get; } = 1.5f;

    public float MaxDistance { get; } = 400f;

    public override void Look(float deltaYaw, float deltaPitch)
    {
        _yaw += deltaYaw;
        _pitch = MathHelpers.Clamp(_pitch + deltaPitch, -MaxPitchRadians, MaxPitchRadians);
        SyncFromAngles();
    }

    public override void Move(Vector3 direction, float deltaSeconds)
    {
        if (direction == Vector3.Zero || deltaSeconds <= 0f)
        {
            return;
        }

        Pivot += Vector3.Normalize(direction) * (Distance * SpeedMultiplier * 0.75f * deltaSeconds);
        SyncFromAngles();
    }

    public override void Zoom(float delta)
    {
        Distance = MathHelpers.Clamp(Distance - delta, MinDistance, MaxDistance);
        SyncFromAngles();
    }

    public override void Reset()
    {
        Pivot = new Vector3(0f, 2f, 0f);
        Distance = 22f;
        _yaw = -0.9f;
        _pitch = -0.55f;
        SyncFromAngles();
    }

    private void SyncFromAngles()
    {
        float horizontal = MathF.Cos(_pitch) * Distance;
        Position = Pivot - new Vector3(
            MathF.Cos(_yaw) * horizontal,
            MathF.Sin(_pitch) * Distance,
            MathF.Sin(_yaw) * horizontal);
        Target = Pivot;
        Up = Vector3.Up;
    }
}
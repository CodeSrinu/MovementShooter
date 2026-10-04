using System;
using Microsoft.Xna.Framework;
using MovementShooter.Core;

namespace MovementShooter.Camera;

/// <summary>
/// Free-flying spectator camera. This is the bootstrap's viewpoint: it lets a designer fly around the
/// arena and judge scale before a player controller exists.
/// </summary>
public sealed class FlyCamera : CameraRig
{
    private readonly float _defaultSpeed;
    private float _yaw = MathHelper.Pi;
    private float _pitch = -0.18f;

    public FlyCamera(float fieldOfViewDegrees, float nearPlane, float farPlane, float moveSpeed)
        : base(fieldOfViewDegrees, nearPlane, farPlane)
    {
        _defaultSpeed = moveSpeed;
        Reset();
    }

    public override string Name => "Fly (spectator)";

    public float MoveSpeed { get; private set; }

    public override void Look(float deltaYaw, float deltaPitch)
    {
        _yaw += deltaYaw;
        _pitch = MathHelpers.Clamp(_pitch + deltaPitch, -MaxPitchRadians, MaxPitchRadians);
        SyncTargetFromAngles();
    }

    public override void Move(Vector3 direction, float deltaSeconds)
    {
        if (direction == Vector3.Zero || deltaSeconds <= 0f)
        {
            return;
        }

        Position += Vector3.Normalize(direction) * (MoveSpeed * SpeedMultiplier * deltaSeconds);
    }

    public override void Zoom(float delta)
    {
        // The wheel doubles as a throttle control while flying.
        MoveSpeed = MathHelpers.Clamp(MoveSpeed + (delta * 2f), 2f, 200f);
    }

    public override void Reset()
    {
        MoveSpeed = _defaultSpeed;
        Position = new Vector3(0f, 6f, 34f);
        _yaw = MathHelper.Pi;
        _pitch = -0.18f;
        SyncTargetFromAngles();
    }

    private void SyncTargetFromAngles()
    {
        Vector3 forward = new(
            MathF.Cos(_pitch) * MathF.Sin(_yaw),
            MathF.Sin(_pitch),
            MathF.Cos(_pitch) * MathF.Cos(_yaw));
        Target = Position + forward;
    }
}
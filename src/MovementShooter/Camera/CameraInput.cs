using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MovementShooter.Camera;

/// <summary>
/// Polls raw keyboard/mouse state into a per-frame snapshot. Keeping this separate from
/// <see cref="CameraController"/> means input can later be fed by a gamepad, a replay or an automated
/// self-test without touching camera maths.
/// </summary>
/// <remarks>
/// Bootstrap bindings (they will be replaced by the real player controller):
/// <list type="bullet">
/// <item>Mouse - look</item>
/// <item>WASD - fly along the ground plane, Q/E - down/up</item>
/// <item>Shift - boost, wheel - throttle</item>
/// <item>F1 - switch camera, R - reset view</item>
/// </list>
/// </remarks>
public sealed class CameraInput
{
    private Vector2 _previousMousePosition;
    private bool _hasPreviousMousePosition;

    public CameraInput(float mouseSensitivity)
    {
        MouseSensitivity = mouseSensitivity;
    }

    public float MouseSensitivity { get; }

    /// <summary>Yaw delta in radians accumulated since the last poll.</summary>
    public float LookDeltaYaw { get; private set; }

    /// <summary>Pitch delta in radians accumulated since the last poll.</summary>
    public float LookDeltaPitch { get; private set; }

    public float ZoomDelta { get; private set; }

    /// <summary>Desired movement in camera-local space: X = strafe, Y = vertical, Z = forward.</summary>
    public Vector3 MoveDirection { get; private set; }

    public bool Boost { get; private set; }

    public bool ToggleCameraRequested { get; private set; }

    public bool ResetRequested { get; private set; }

    public void Poll(bool acceptMouseLook)
    {
        KeyboardState keyboard = Keyboard.GetState();

        MoveDirection = ReadMoveDirection(keyboard);
        Boost = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        ToggleCameraRequested = keyboard.IsKeyDown(Keys.F1);
        ResetRequested = keyboard.IsKeyDown(Keys.R);

        MouseState mouse = Mouse.GetState();
        LookDeltaYaw = 0f;
        LookDeltaPitch = 0f;
        ZoomDelta = mouse.ScrollWheelValue;

        if (acceptMouseLook)
        {
            Vector2 position = new(mouse.X, mouse.Y);
            if (_hasPreviousMousePosition)
            {
                Vector2 delta = position - _previousMousePosition;
                LookDeltaYaw = delta.X * MouseSensitivity;
                LookDeltaPitch = -delta.Y * MouseSensitivity;
            }

            _previousMousePosition = position;
            _hasPreviousMousePosition = true;
        }
        else
        {
            _hasPreviousMousePosition = false;
        }
    }

    private static Vector3 ReadMoveDirection(KeyboardState keyboard)
    {
        Vector3 direction = Vector3.Zero;

        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
        {
            direction.Z += 1f;
        }

        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
        {
            direction.Z -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.D))
        {
            direction.X += 1f;
        }

        if (keyboard.IsKeyDown(Keys.A))
        {
            direction.X -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.E))
        {
            direction.Y += 1f;
        }

        if (keyboard.IsKeyDown(Keys.Q))
        {
            direction.Y -= 1f;
        }

        return direction;
    }
}
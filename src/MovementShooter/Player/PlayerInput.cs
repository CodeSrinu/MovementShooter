using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MovementShooter.Player;

/// <summary>What the player is asking for this frame, independent of devices.</summary>
public readonly record struct PlayerInputState(
    Vector2 Move,
    bool JumpPressed,
    bool SprintHeld,
    bool ToggleCursorRequested,
    bool SlideHeld = false,
    bool DashPressed = false);

/// <summary>
/// Turns the keyboard into a <see cref="PlayerInputState"/>. Mouse look lives in <see cref="CursorLock"/>
/// because it needs the captured cursor position. Separating input from the controller means movement can
/// be driven by a bot or a replay later without touching movement code.
/// </summary>
/// <remarks>
/// Bindings: WASD move, Space jump, Left Ctrl slide, Left Shift dash, F1 release or capture the cursor.
/// </remarks>
public sealed class PlayerInput
{
    public PlayerInputState Poll()
    {
        KeyboardState keyboard = Keyboard.GetState();

        Vector2 move = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.W))
        {
            move.Y += 1f;
        }

        if (keyboard.IsKeyDown(Keys.S))
        {
            move.Y -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.D))
        {
            move.X += 1f;
        }

        if (keyboard.IsKeyDown(Keys.A))
        {
            move.X -= 1f;
        }

        if (move.LengthSquared() > 1f)
        {
            move = Vector2.Normalize(move);
        }

        return new PlayerInputState(
            move,
            keyboard.IsKeyDown(Keys.Space),
            keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift),
            keyboard.IsKeyDown(Keys.F1),
            keyboard.IsKeyDown(Keys.LeftControl),
            keyboard.IsKeyDown(Keys.LeftShift));
    }
}
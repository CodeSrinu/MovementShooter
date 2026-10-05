using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MovementShooter.Player;

namespace MovementShooter.Weapons;

/// <summary>What the player is asking the weapon to do this frame.</summary>
public readonly record struct WeaponInputState(bool FirePressed);

/// <summary>
/// Turns devices into a <see cref="WeaponInputState"/>. Separate from <c>PlayerInput</c> because weapon input
/// has a different lifecycle: movement is polled continuously and latched per fixed substep, whereas fire is an
/// edge that the cursor-lock rules must not interfere with.
/// </summary>
public sealed class WeaponInput
{
    /// <summary>
    /// Reads the fire input.
    ///
    /// Left mouse button only, and only while the cursor is captured. That second condition is what stops the
    /// click that *captures* the cursor from also firing the weapon: <see cref="CursorLock.Update"/> recaptures on
    /// a left click, and without this guard the shot that brought the cursor back would be a free shot the
    /// player never aimed.
    /// </summary>
    public WeaponInputState Poll(bool cursorLocked)
    {
        bool held = Mouse.GetState().LeftButton == ButtonState.Pressed;
        return new WeaponInputState(cursorLocked && held);
    }
}

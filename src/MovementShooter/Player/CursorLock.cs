using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MovementShooter.Player;

/// <summary>
/// First-person mouse capture. MonoGame has no relative-mouse mode, so capture is implemented the usual
/// way: hide the cursor, and every frame read its position and move it back to the centre of the viewport.
/// The offset from centre is the look delta.
///
/// Escape releases the cursor; clicking while released captures it again.
/// </summary>
public sealed class CursorLock
{
    private const int RecentreDelayMilliseconds = 30;

    private readonly Action<bool> _setCursorVisible;
    private readonly Func<Point> _viewportCentre;
    private readonly Func<bool> _leftMousePressed;
    private bool _isLocked;
    private bool _wasLockedWhenDeactivated;

    public CursorLock(Action<bool> setCursorVisible, Func<Point> viewportCentre, Func<bool> leftMousePressed)
    {
        _setCursorVisible = setCursorVisible;
        _viewportCentre = viewportCentre;
        _leftMousePressed = leftMousePressed;
    }

    public bool IsLocked => _isLocked;

    /// <summary>Captures or releases the cursor. Gameplay input is ignored while released.</summary>
    public void Toggle()
    {
        SetLocked(!_isLocked);
    }

    public void SetLocked(bool locked)
    {
        if (_isLocked == locked)
        {
            return;
        }

        _isLocked = locked;
        _setCursorVisible(!locked);
        if (locked)
        {
            Recentre();
        }
    }

    /// <summary>
    /// Consumes this frame's mouse movement. Call once per frame, before gameplay reads it.
    /// </summary>
    public Vector2 ConsumeLookDelta()
    {
        if (!_isLocked)
        {
            return Vector2.Zero;
        }

        Point centre = _viewportCentre();
        Point current = Mouse.GetState().Position;
        Vector2 delta = new(current.X - centre.X, current.Y - centre.Y);

        Recentre();
        return delta;
    }

    /// <summary>Called once per frame: handles click-to-recapture.</summary>
    public void Update()
    {
        if (!_isLocked && _leftMousePressed())
        {
            SetLocked(true);
        }
    }

    /// <summary>
    /// Called when the window loses focus: the cursor is handed back so it is not trapped in a window the
    /// player is not looking at. Focus is remembered so returning to the game recaptures it.
    /// </summary>
    public void HandleWindowDeactivated()
    {
        _wasLockedWhenDeactivated = _isLocked;
        SetLocked(false);
    }

    /// <summary>Called when the window regains focus: recapture only if we held the cursor before.</summary>
    public void HandleWindowActivated()
    {
        if (_wasLockedWhenDeactivated)
        {
            _wasLockedWhenDeactivated = false;
            SetLocked(true);
        }
    }

    private void Recentre()
    {
        Point centre = _viewportCentre();
        Mouse.SetPosition(centre.X, centre.Y);
        _ = RecentreDelayMilliseconds;
    }
}
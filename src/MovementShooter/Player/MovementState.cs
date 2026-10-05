namespace MovementShooter.Player;

/// <summary>
/// Which movement behaviour is currently driving the player. This is a small vocabulary, not a state
/// machine: <see cref="PlayerMovement"/> keeps owning all the physics, and the state exists so the debug
/// readout, the camera and the tests can talk about what the player is doing without inspecting velocity.
/// </summary>
public enum MovementState
{
    /// <summary>On a walkable surface, under full movement control.</summary>
    Grounded,

    /// <summary>Crouched and committed to a momentum-driven slide.</summary>
    Sliding,

    /// <summary>Off the ground, under air control.</summary>
    Airborne,
}

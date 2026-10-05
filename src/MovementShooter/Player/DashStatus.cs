using System;

namespace MovementShooter.Player;

/// <summary>
/// The dash is an <b>ability</b>, not a movement state, so it gets its own small vocabulary instead of a fourth
/// <see cref="MovementState"/>. It fires as a one-off burst and the player is immediately back under the normal
/// rules for whichever state they were in, which is what keeps dash from turning movement into a state machine.
/// </summary>
public readonly record struct DashStatus(
    bool IsAvailable,
    bool AirDashUsed,
    float CooldownRemaining,
    int DashesThisAirbornePeriod)
{
    /// <summary>True while the dash key would do nothing.</summary>
    public bool IsLocked => !IsAvailable;
}

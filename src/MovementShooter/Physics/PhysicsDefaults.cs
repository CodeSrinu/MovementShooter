namespace MovementShooter.Physics;

/// <summary>
/// Physics-engine constants that are not gameplay tuning. Gameplay feel values live with the system that
/// owns them (for example jump speed in <c>Player.PlayerTuning</c>).
/// </summary>
public static class PhysicsDefaults
{
    /// <summary>Contact spring frequency. Higher is stiffer; too high and resting contacts jitter.</summary>
    public const float ContactSpringFrequency = 30f;

    /// <summary>How fast the solver may push a body out of penetration, in metres per second.</summary>
    public const float MaximumRecoveryVelocity = 2f;

    /// <summary>Fixed simulation step. Gameplay runs in whole multiples of this, which is what makes
    /// movement behave identically at 30, 60 and 240 frames per second.</summary>
    public const float FixedTimeStep = 1f / 60f;

    /// <summary>Upper bound on catch-up steps per frame, so a long stall cannot spiral.</summary>
    public const int MaximumSubStepsPerFrame = 5;

    public const int SolverVelocityIterations = 1;
    public const int SolverSubStepCount = 1;
}
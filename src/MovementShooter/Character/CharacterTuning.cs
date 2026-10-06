using System;

namespace MovementShooter.Character;

/// <summary>
/// Presentation tuning for the full-body character.
///
/// Every number here is presentation only. None of it can reach movement, collision or
/// camera aim - the character reads the simulation and never writes to it.
/// </summary>
public sealed class CharacterTuning
{
    /// <summary>Uniform scale applied to the authored 1.795 m character.</summary>
    /// <remarks>
    /// The rig was authored to match the player capsule height, so this is 1.0 by default
    /// and exists as an explicit knob rather than a magic number at the call site.
    /// </remarks>
    public float Scale { get; set; } = 1f;

    /// <summary>Ground speed, in m/s, at which the run clip plays at its authored rate.</summary>
    public float RunSpeedForFullRate { get; set; } = 6.5f;

    /// <summary>Cross-fade length when the clip changes, in seconds.</summary>
    public float BlendSeconds { get; set; } = 0.11f;

    /// <summary>Validates the tuning and throws rather than rendering a broken character.</summary>
    public void Validate()
    {
        if (Scale is <= 0f or > 10f)
        {
            throw new ArgumentOutOfRangeException(nameof(Scale), Scale, "Character scale must be in (0, 10].");
        }

        if (RunSpeedForFullRate <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(RunSpeedForFullRate), RunSpeedForFullRate,
                "Run speed for full rate must be positive.");
        }

        if (BlendSeconds < 0f || BlendSeconds > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(BlendSeconds), BlendSeconds,
                "Blend length must be between 0 and 1 second.");
        }
    }
}

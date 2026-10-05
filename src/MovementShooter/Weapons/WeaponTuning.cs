using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Weapons;

/// <summary>
/// Per-weapon tuning. Every number a weapon needs lives in one of these, never inline in firing code, so a
/// weapon is defined entirely by data.
/// </summary>
/// <remarks>
/// Deliberately separate from <c>PlayerTuning</c>. Movement feel and weapon feel are tuned independently and
/// changing one must not be able to alter the other - a rocket's speed has no business next to the player's
/// run speed.
/// </remarks>
public class WeaponTuning
{
    /// <summary>Name shown in the debug readout.</summary>
    public string Name { get; set; } = "Weapon";

    /// <summary>Seconds between shots. Zero allows firing on every step.</summary>
    public float FireInterval { get; set; }

    /// <summary>How far in front of the muzzle the projectile appears, in metres.</summary>
    public float MuzzleOffset { get; set; } = 0.6f;
}

/// <summary>
/// Rocket launcher tuning. The values the milestone calls for, plus the two that make self-damage a trade
/// rather than a punishment.
/// </summary>
public sealed class RocketLauncherTuning : WeaponTuning
{
    public RocketLauncherTuning()
    {
        Name = "Rocket Launcher";

        // A launcher, not a rapid-fire weapon. Deliberately a little longer than the dash cooldown, so the two
        // are never confused on the same key.
        FireInterval = 0.75f;

        // Far enough ahead of the camera that the rocket does not appear to spawn inside the player's own head.
        MuzzleOffset = 0.8f;
    }

    /// <summary>Constant speed of the projectile in metres per second.</summary>
    public float RocketSpeed { get; set; } = 42f;

    /// <summary>Seconds a rocket may exist before it detonates anyway, so nothing flies off forever.</summary>
    public float RocketLifetime { get; set; } = 5f;

    /// <summary>Radius of the projectile in metres. Used for the visible body and for sweep margin.</summary>
    public float RocketRadius { get; set; } = 0.12f;

    /// <summary>Radius within which the explosion affects anything.</summary>
    public float ExplosionRadius { get; set; } = 5.5f;

    /// <summary>Damage at the exact centre of the blast, before falloff.</summary>
    public float ExplosionDamage { get; set; } = 120f;

    /// <summary>
    /// Impulse at the exact centre of the blast, before falloff, in newton-seconds. Mass-scaled by the engine, so
    /// this is what a player of <see cref="Player.PlayerTuning.Mass"/> actually feels.
    /// </summary>
    public float ExplosionForce { get; set; } = 900f;

    /// <summary>
    /// Damage the firer takes at the centre, as a fraction of <see cref="ExplosionDamage"/>. Below 1: a rocket
    /// jump is meant to be a choice between height and health, not a trap.
    /// </summary>
    public float SelfDamage { get; set; } = 0.45f;

    /// <summary>
    /// Impulse the firer takes at the centre, as a fraction of <see cref="ExplosionForce"/>. Full, because the
    /// movement is the point and the self-damage is the price.
    /// </summary>
    public float SelfKnockback { get; set; } = 1f;
}

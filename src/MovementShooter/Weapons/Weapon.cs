using System;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;
using MovementShooter.Physics;

namespace MovementShooter.Weapons;

/// <summary>What a weapon needs in order to fire. Deliberately not the player.</summary>
/// <param name="Origin">Where the projectile should appear.</param>
/// <param name="Direction">Unit direction it should travel.</param>
/// <param name="Owner">The target that fired, so its own blast is recognised as self-damage.</param>
public readonly record struct FireRequest(Vector3 Origin, Vector3 Direction, CombatTarget? Owner);

/// <summary>
/// One shot's worth of output. A weapon returns these rather than spawning anything itself, which is what
/// keeps weapon logic testable and lets the caller decide what a shot means - a projectile, a hitscan ray, a
/// beam. Hitscan is deliberately not implemented; the milestone's rocket must physically travel.
/// </summary>
public readonly record struct Shot(Vector3 Origin, Vector3 Direction, float Speed, float Radius, float Lifetime, WeaponTuning Tuning);

/// <summary>
/// Base class for anything the player can fire. Owns its own cooldown and nothing else: where a shot goes and
/// what it does belongs to the subclass, and who owns the weapon belongs to the player.
/// </summary>
public abstract class Weapon
{
    private float _cooldownRemaining;

    protected Weapon(WeaponTuning tuning)
    {
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
    }

    public WeaponTuning Tuning { get; }

    /// <summary>True while the weapon cannot fire.</summary>
    public bool IsCoolingDown => _cooldownRemaining > 0f;

    public float CooldownRemaining => _cooldownRemaining;

    /// <summary>Shots fired since the weapon was created. Diagnostics only.</summary>
    public int ShotsFired { get; private set; }

    /// <summary>Advances the cooldown. Called once per fixed step.</summary>
    public void Step(float deltaSeconds) =>
        _cooldownRemaining = MathF.Max(0f, _cooldownRemaining - deltaSeconds);

    /// <summary>
    /// Attempts to fire. Returns null when the weapon is still cooling down, which is the only reason a fire
    /// attempt is ever refused - there is no ammo and no magazine yet.
    /// </summary>
    public Shot? TryFire(in FireRequest request)
    {
        if (_cooldownRemaining > 0f)
        {
            return null;
        }

        if (request.Direction.LengthSquared() < 1e-8f)
        {
            return null;
        }

        _cooldownRemaining = Tuning.FireInterval;
        ShotsFired++;

        return BuildShot(in request);
    }

    /// <summary>Builds the shot for this weapon, in the given direction.</summary>
    protected abstract Shot? BuildShot(in FireRequest request);

    /// <summary>
    /// Clears the cooldown. Development-only, so a test or the debug key can fire again immediately.
    /// </summary>
    public void ResetCooldown() => _cooldownRemaining = 0f;
}

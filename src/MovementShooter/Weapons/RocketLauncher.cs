using System;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;

namespace MovementShooter.Weapons;

/// <summary>
/// The rocket launcher. Produces a shot travelling at <see cref="RocketLauncherTuning.RocketSpeed"/>; what
/// happens to it afterwards - travel, collision, detonation - belongs to <see cref="ProjectileSystem"/>.
///
/// The weapon itself holds no projectile state. That separation is what lets the same projectile code serve a
/// grenade or a mine later with no changes here.
/// </summary>
public sealed class RocketLauncher : Weapon
{
    public RocketLauncher(RocketLauncherTuning tuning)
        : base(tuning)
    {
    }

    public new RocketLauncherTuning Tuning => (RocketLauncherTuning)base.Tuning;

    protected override Shot? BuildShot(in FireRequest request)
    {
        Vector3 direction = Vector3.Normalize(request.Direction);

        return new Shot(
            request.Origin + (direction * Tuning.MuzzleOffset),
            direction,
            Tuning.RocketSpeed,
            Tuning.RocketRadius,
            Tuning.RocketLifetime,
            Tuning);
    }

    /// <summary>Builds the explosion this weapon's rockets produce, centred where the rocket detonated.</summary>
    public Explosion ExplosionFor(Vector3 centre, CombatTarget? source) => new(
        centre,
        Tuning.ExplosionRadius,
        Tuning.ExplosionDamage,
        Tuning.ExplosionForce,
        source)
    {
        SelfDamageScale = Tuning.SelfDamage,
        SelfKnockbackScale = Tuning.SelfKnockback,
        Label = "rocket",
    };
}

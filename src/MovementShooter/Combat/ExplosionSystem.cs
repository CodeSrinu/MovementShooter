using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Physics;

namespace MovementShooter.Combat;

/// <summary>
/// Something an explosion can find, damage, and push. A target pairs a body to knock around with an optional
/// <see cref="IDamageable"/>, so an indestructible prop and a fragile one are the same object with a null field.
/// </summary>
public sealed class CombatTarget
{
    public CombatTarget(string name, PhysicsBody? body, IDamageable? damageable = null, object? owner = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (body is null && damageable is null)
        {
            throw new ArgumentException("A combat target needs a body to push or something to damage.", nameof(body));
        }

        Name = name;
        Body = body;
        Damageable = damageable;
        Owner = owner ?? damageable;
    }

    /// <summary>Label used by the debug readout and test failure messages.</summary>
    public string Name { get; }

    /// <summary>The body an impulse is applied to, or null for a target that can only be damaged.</summary>
    public PhysicsBody? Body { get; }

    public IDamageable? Damageable { get; }

    /// <summary>
    /// What this target belongs to, used to recognise self-damage. An explosion whose source matches a target's
    /// owner is hitting the firer, and that is the only thing that distinguishes self-damage from ordinary
    /// collateral - there is no per-weapon list of who counts as an enemy, because there are no teams yet.
    /// </summary>
    public object? Owner { get; }

    /// <summary>
    /// Where the explosion should measure from. The body's centre by default, which for the player is the
    /// middle of the capsule and not its feet - an explosion on the floor therefore pushes a grounded player
    /// up and slightly away, rather than only up.
    /// </summary>
    public Vector3 Centre => Body?.Position ?? Vector3.Zero;
}

/// <summary>A single explosion, described before it happens.</summary>
/// <param name="Centre">Where it happens.</param>
/// <param name="Radius">Outer edge. Nothing beyond it is affected at all.</param>
/// <param name="Damage">Damage at the exact centre, before falloff.</param>
/// <param name="Force">Impulse at the exact centre, before falloff and before mass.</param>
/// <param name="Source">The target that caused this, used to recognise self-damage. May be null.</param>
public readonly record struct Explosion(
    Vector3 Centre,
    float Radius,
    float Damage,
    float Force,
    CombatTarget? Source)
{
    /// <summary>
    /// Damage dealt to the firer at the centre, as a fraction of <see cref="Damage"/>. Below 1 so that a
    /// rocket jump is a real trade: more height, but at a cost that scales the same way the height does.
    /// </summary>
    public float SelfDamageScale { get; init; } = 1f;

    /// <summary>
    /// Impulse dealt to the firer at the centre, as a fraction of <see cref="Force"/>. Full by default - the
    /// movement *is* the reward, and the self-damage is the price.
    /// </summary>
    public float SelfKnockbackScale { get; init; } = 1f;

    /// <summary>Short label for the debug readout.</summary>
    public string Label { get; init; } = "explosion";
}

/// <summary>What one explosion did to one target.</summary>
public readonly record struct BlastResult(
    CombatTarget Target,
    float Distance,
    float Strength,
    float DamageDealt,
    Vector3 Impulse,
    bool IsSelfDamage)
{
    public bool WasAffected => Strength > 0f;
}

/// <summary>
/// The reusable explosion: finds every registered target within the radius, works out falloff, and applies
/// damage and knockback through the normal paths.
///
/// It is a system rather than a method on any weapon. A grenade, a rocket and a future mine all call the same
/// <see cref="Detonate"/>, and there is exactly one implementation of "what an explosion does" in the codebase.
/// </summary>
/// <remarks>
/// Targets are held in a plain list and scanned linearly. There are a handful of them; a spatial index would be
/// scaffolding for a problem that does not exist yet, and it would have to be kept correct as targets move.
/// </remarks>
public sealed class ExplosionSystem
{
    private readonly List<CombatTarget> _targets = new();

    /// <summary>Every registered target, in registration order.</summary>
    public IReadOnlyList<CombatTarget> Targets => _targets;

    /// <summary>
    /// Seconds of simulation time explosions have been timestamped against. Set from
    /// <see cref="Detonate"/>'s <c>elapsedSeconds</c> argument, which the caller supplies from the fixed-step
    /// clock - so this is simulation time, not wall-clock time, and the last explosion's age is the same at any
    /// frame rate.
    /// </summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>Explosions detonated so far. Diagnostics and tests read this rather than counting separately.</summary>
    public int Detonations { get; private set; }

    /// <summary>The most recent explosion, for the debug readout. Null before anything has blown up.</summary>
    public Explosion? LastExplosion { get; private set; }

    /// <summary>When and where the most recent explosion happened, and how far it reached.</summary>
    public float LastExplosionSeconds { get; private set; } = -1f;

    public Vector3 LastExplosionCentre { get; private set; }

    public float LastExplosionRadius { get; private set; }

    /// <summary>Registers a target. Registering the same instance twice would double every explosion's effect.</summary>
    public CombatTarget Register(CombatTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!_targets.Contains(target))
        {
            _targets.Add(target);
        }

        return target;
    }

    public bool Unregister(CombatTarget target) => _targets.Remove(target);

    public void ClearTargets() => _targets.Clear();

    /// <summary>
    /// Detonates <paramref name="explosion"/> and returns what it did to every target, in registration order.
    ///
    /// Damage and impulse are computed by the same falloff for every target, and the only difference between the
    /// firer and anyone else is the two scale factors on the explosion itself. Self-damage therefore goes through
    /// exactly the same <see cref="IDamageable.TakeDamage"/> and the same impulse call as a bystander - there is
    /// no separate self-damage path that could drift out of step with the ordinary one.
    /// </summary>
    public IReadOnlyList<BlastResult> Detonate(in Explosion explosion, float elapsedSeconds)
    {
        List<BlastResult> results = new(_targets.Count);

        ElapsedSeconds = elapsedSeconds;
        Detonations++;
        LastExplosion = explosion;
        LastExplosionSeconds = elapsedSeconds;
        LastExplosionCentre = explosion.Centre;
        LastExplosionRadius = explosion.Radius;

        foreach (CombatTarget target in _targets)
        {
            Vector3 centre = target.Centre;
            float distance = Vector3.Distance(explosion.Centre, centre);
            float strength = ExplosionFalloff.Strength(distance, explosion.Radius);

            if (strength <= 0f)
            {
                results.Add(new BlastResult(target, distance, 0f, 0f, Vector3.Zero, IsSelf(target, explosion)));
                continue;
            }

            bool isSelf = IsSelf(target, explosion);

            float damageScale = isSelf ? explosion.SelfDamageScale : 1f;
            float forceScale = isSelf ? explosion.SelfKnockbackScale : 1f;

            float damage = explosion.Damage * strength * damageScale;

            if (target.Damageable is not null && damage > 0f)
            {
                target.Damageable.TakeDamage(DamageInfo.From(
                    explosion.Source?.Owner,
                    explosion.Centre,
                    damage,
                    isSelf));
            }

            Vector3 impulse = ExplosionFalloff.Impulse(
                explosion.Centre,
                centre,
                explosion.Force * forceScale,
                explosion.Radius);

            // The impulse is applied through the body's own API rather than by writing velocity, so it is
            // mass-scaled and it *adds* to whatever the body was already doing. A player who was running keeps
            // running; a player who was falling keeps falling. Nothing here can replace momentum.
            target.Body?.ApplyImpulse(impulse);

            results.Add(new BlastResult(target, distance, strength, damage, impulse, isSelf));
        }

        return results;
    }

    private static bool IsSelf(CombatTarget target, in Explosion explosion) =>
        explosion.Source is not null && ReferenceEquals(target.Owner, explosion.Source.Owner);
}

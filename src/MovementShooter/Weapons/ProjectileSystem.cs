using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;
using MovementShooter.Physics;

namespace MovementShooter.Weapons;

/// <summary>
/// A projectile in flight: a position, a velocity, a lifetime, and who fired it.
///
/// A struct in a pooled list. Projectiles are short-lived and numerous, and the system never allocates while
/// they are alive - an allocation per shot would be a GC pause in the middle of a firefight, which is precisely
/// when a dropped frame is worst.
/// </summary>
public struct Projectile
{
    /// <summary>World position.</summary>
    public Vector3 Position;

    /// <summary>World velocity. Constant for a rocket: it does not slow down or fall.</summary>
    public Vector3 Velocity;

    /// <summary>Seconds remaining before it detonates on its own.</summary>
    public float LifetimeRemaining;

    /// <summary>Radius, used for the visible body and as sweep margin against thin geometry.</summary>
    public float Radius;

    /// <summary>Who fired it. Null for a projectile with no owner.</summary>
    public CombatTarget? Owner;

    /// <summary>True while the projectile is live. Cleared the moment it detonates.</summary>
    public bool IsActive;

    /// <summary>The weapon tuning this was fired from, so the explosion matches the shot.</summary>
    public WeaponTuning? Tuning;
}

/// <summary>
/// Owns every live projectile and steps them.
///
/// Movement is the *only* source of collision detection: each step sweeps from the projectile's previous
/// position to its new one and asks the physics world what it crossed. A point test at the new position would
/// tunnel straight through a wall at 42 m/s - at a 1/60 s step that is 0.7 m per step, and the arena has 0.6 m
/// thick platforms. Sweeping cannot miss, because the whole path is tested, whatever the speed.
/// </summary>
/// <remarks>
/// Stepping happens in the physics world's pre-step callback so a projectile advances in exactly the same fixed
/// steps as the player, and so the sweep sees a world that has not yet been integrated this step.
/// </remarks>
public sealed class ProjectileSystem
{
    private readonly PhysicsWorld _physics;
    private readonly ExplosionSystem _explosions;
    private readonly List<Projectile> _live = new();

    /// <summary>Direction used when a rocket's centre lands exactly on a target's centre.</summary>
    private const float MinimumDistanceForDirection = 1e-4f;

    public ProjectileSystem(PhysicsWorld physics, ExplosionSystem explosions, RocketLauncher? launchers = null)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _explosions = explosions ?? throw new ArgumentNullException(nameof(explosions));

        if (launchers is not null)
        {
            _launchers.Add(launchers);
        }
    }

    /// <summary>
    /// Weapons whose rockets detonate on impact. A projectile carries its own tuning, but it is the weapon - not
    /// the projectile - that knows how to turn its numbers into an <see cref="Explosion"/>, so the system asks.
    /// Registering the weapon is what connects firing to detonation without the projectile knowing anything
    /// about damage.
    /// </summary>
    private readonly List<RocketLauncher> _launchers = new();

    /// <summary>Registers a weapon whose rockets this system will detonate.</summary>
    public ProjectileSystem Register(RocketLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        if (!_launchers.Contains(launcher))
        {
            _launchers.Add(launcher);
        }

        return this;
    }

    /// <summary>Projectiles currently in flight.</summary>
    public int LiveCount => _live.Count;

    /// <summary>Projectiles ever fired.</summary>
    public int TotalSpawned { get; private set; }

    /// <summary>Projectiles that ended by hitting something.</summary>
    public int TotalImpacts { get; private set; }

    /// <summary>Projectiles that ended because their lifetime ran out.</summary>
    public int TotalExpirations { get; private set; }

    /// <summary>Seconds of simulation elapsed, so explosions can be timestamped for the readout.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>Read-only view of the live projectiles, for rendering and tests.</summary>
    public IReadOnlyList<Projectile> Live => _live;

    /// <summary>Convenience for tests: the first live projectile's position, or false if there are none.</summary>
    public bool TryGetFirst(out Projectile projectile)
    {
        if (_live.Count > 0)
        {
            projectile = _live[0];
            return true;
        }

        projectile = default;
        return false;
    }

    /// <summary>Spawns a projectile from a weapon's shot.</summary>
    public Projectile Spawn(in Shot shot, CombatTarget? owner)
    {
        Projectile projectile = new()
        {
            Position = shot.Origin,
            Velocity = shot.Direction * shot.Speed,
            LifetimeRemaining = shot.Lifetime,
            Radius = shot.Radius,
            Owner = owner,
            IsActive = true,
            Tuning = shot.Tuning,
        };

        _live.Add(projectile);
        TotalSpawned++;
        return projectile;
    }

    /// <summary>Advances every projectile by one fixed step, detonating any that hit or expire.</summary>
    public void Step(float deltaSeconds)
    {
        ElapsedSeconds += deltaSeconds;

        for (int i = _live.Count - 1; i >= 0; i--)
        {
            Projectile projectile = _live[i];
            Vector3 from = projectile.Position;
            Vector3 travel = projectile.Velocity * deltaSeconds;
            Vector3 to = from + travel;

            // The sweep covers the whole path this step. The margin is the projectile's own radius: the body
            // would have touched geometry slightly before its centre reached the surface, and a rocket that
            // stops at the surface rather than inside it reads as hitting the wall it actually hit.
            float sweep = travel.Length() + projectile.Radius;

            RayHit hit = _physics.RaycastStatic(from, Vector3.Normalize(travel), sweep);

            if (hit.Hit && hit.Distance <= sweep)
            {
                // Back the hit point up out of the surface by the projectile radius so the blast happens at the
                // point of contact, not buried inside the wall.
                Vector3 surfacePoint = from + (Vector3.Normalize(travel) * hit.Distance);
                Vector3 centre = surfacePoint - (hit.Normal * projectile.Radius);

                Detonate(ref projectile, centre, countImpact: true);
                _live.RemoveAt(i);
                continue;
            }

            projectile.Position = to;
            projectile.LifetimeRemaining -= deltaSeconds;

            if (projectile.LifetimeRemaining <= 0f)
            {
                Detonate(ref projectile, to, countImpact: false);
                _live.RemoveAt(i);
                continue;
            }

            _live[i] = projectile;
        }
    }

    /// <summary>
    /// Detonates a projectile at a point: hands the explosion to the shared <see cref="ExplosionSystem"/> and
    /// marks the projectile dead. There is no weapon-specific behaviour in here and no knowledge of what the
    /// target is - the explosion system applies the same damage and the same impulse to the firer as to anyone
    /// else, which is what makes a rocket jump fall out of the ordinary path rather than being written
    /// separately.
    /// </summary>
    private void Detonate(ref Projectile projectile, Vector3 centre, bool countImpact)
    {
        projectile.IsActive = false;

        foreach (RocketLauncher launcher in _launchers)
        {
            if (ReferenceEquals(launcher.Tuning, projectile.Tuning))
            {
                _explosions.Detonate(launcher.ExplosionFor(centre, projectile.Owner), ElapsedSeconds);
                break;
            }
        }

        if (countImpact)
        {
            TotalImpacts++;
        }
        else
        {
            TotalExpirations++;
        }
    }

    /// <summary>Removes every live projectile without detonating. Used between test scenarios.</summary>
    public void Clear() => _live.Clear();
}

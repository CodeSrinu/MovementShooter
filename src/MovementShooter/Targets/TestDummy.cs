using System;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;
using MovementShooter.Physics;

namespace MovementShooter.Targets;

/// <summary>
/// A static target for testing explosion damage, knockback and falloff: a body the blast can throw, a health
/// pool to drain, and a bright box so it can be seen from across the arena.
///
/// Deliberately not an enemy. There is no behaviour, no health bar and no AI here - only the three things a
/// rocket needs to demonstrate that it did something, so that a test can assert on them.
/// </summary>
public sealed class TestDummy : IDisposable
{
    private bool _disposed;

    public TestDummy(PhysicsWorld physics, string name, Vector3 position, float size = 1.2f, float mass = 60f, float maxHealth = 100f)
    {
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentException.ThrowIfNullOrEmpty(name);

        Name = name;
        Size = size;

        // A dynamic capsule rather than a static box, on purpose: a static body could not be knocked back, and
        // knockback is half of what this target exists to demonstrate. The player is also a dynamic capsule, so
        // the masses and response are comparable.
        Body = physics.AddDynamicCapsule(size * 0.5f, 0.01f, position, mass);
        Health = new Health(maxHealth);

        Target = new CombatTarget(name, Body, Health, this);
    }

    public string Name { get; }

    /// <summary>Edge length of the dummy's roughly-cubic capsule, used for rendering.</summary>
    public float Size { get; }

    public PhysicsBody Body { get; }

    public Health Health { get; }

    /// <summary>The registered combat target, which is what an explosion system holds.</summary>
    public CombatTarget Target { get; }

    public Vector3 Position => Body.Position;

    public Vector3 Velocity => Body.LinearVelocity;

    public float CurrentHealth => Health.CurrentHealth;

    /// <summary>Puts the dummy back where it started, at rest and at full health.</summary>
    public void Reset(Vector3 position, float maxHealth)
    {
        Health.Reset();
        Body.Teleport(position);
        _ = maxHealth;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Body.Dispose();
    }
}

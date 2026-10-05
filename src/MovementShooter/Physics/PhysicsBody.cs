using System;
using BepuPhysics;
using Microsoft.Xna.Framework;

namespace MovementShooter.Physics;

/// <summary>
/// A dynamic body in the <see cref="PhysicsWorld"/>. Movement code reads and writes linear velocity
/// directly (that is how a character is driven); impulses are available for future knockback.
/// </summary>
public sealed class PhysicsBody : IDisposable
{
    private readonly PhysicsWorld _world;
    private bool _disposed;

    internal PhysicsBody(PhysicsWorld world, BepuPhysics.BodyHandle handle, float mass)
    {
        _world = world;
        Handle = handle;
        Mass = mass;
    }

    internal BepuPhysics.BodyHandle Handle { get; }

    public Vector3 Position => Reference().Pose.Position.ToGame();

    public Vector3 LinearVelocity
    {
        get => Reference().Velocity.Linear.ToGame();
        set
        {
            // A sleeping body ignores velocity writes, so every write wakes it. This is why the player
            // keeps responding to input even after standing perfectly still.
            _world.Wake(Handle);
            Reference().Velocity.Linear = value.ToNum();
        }
    }

    public CollidableId Collidable => new(Reference().CollidableReference.Packed);

    /// <summary>Mass in kilograms, as given when the body was created.</summary>
    public float Mass { get; }

    /// <summary>
    /// Applies an instantaneous change in momentum, e.g. explosion knockback.
    ///
    /// This is additive by construction. The solver integrates it into whatever velocity the body already has,
    /// so a running player is still running when the blast lands - which is exactly what makes rocket jumping
    /// compose with momentum instead of replacing it. The alternative, writing velocity directly, would
    /// silently discard speed and could even stop a jump mid-air.
    ///
    /// The engine mass-scales it: the argument is an impulse in newton-seconds, so a heavier body moves less for
    /// the same blast.
    /// </summary>
    public void ApplyImpulse(Vector3 impulse)
    {
        _world.Wake(Handle);
        BodyReference reference = Reference();
        reference.ApplyLinearImpulse(impulse.ToNum());
    }

    /// <summary>
    /// The velocity change an impulse of this size produces on this body, without applying it. Used by the
    /// checks to reason about knockback against real masses.
    /// </summary>
    public float VelocityChangeFrom(Vector3 impulse) => impulse.Length() / Mass;

    /// <summary>Moves the body instantly and clears its momentum.</summary>
    public void Teleport(Vector3 position)
    {
        SetPositionKeepingVelocity(position);
        Reference().Velocity.Linear = System.Numerics.Vector3.Zero;
    }

    /// <summary>
    /// Moves the body instantly while preserving its velocity.
    ///
    /// Resizing the capsule has to move the body so the feet stay put, and that must not cancel momentum: a
    /// crouch is a change of shape, not a stop. <see cref="Teleport"/> zeroes velocity and would make every
    /// slide begin from a dead stop.
    /// </summary>
    public void SetPositionKeepingVelocity(Vector3 position)
    {
        _world.Wake(Handle);
        Reference().Pose.Position = position.ToNum();
    }

    private BodyReference Reference() => _world.ReferenceOf(Handle);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _world.Forget(Handle);
    }
}
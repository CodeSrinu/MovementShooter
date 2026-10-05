using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Combat;

/// <summary>
/// What a source did to a target. A value type carrying everything a damage handler might need, so adding a
/// weapon never means widening a method signature.
/// </summary>
/// <param name="Amount">Damage to apply. Negative values are ignored by every handler.</param>
/// <param name="Source">Who caused it, or null for environmental damage. Never the target itself.</param>
/// <param name="WorldPosition">Where the damage came from, so a handler could scale by distance.</param>
/// <param name="IsSelfDamage">True when <paramref name="Source"/> is the target's own owner.</param>
public readonly record struct DamageInfo(
    float Amount,
    object? Source,
    Vector3 WorldPosition,
    bool IsSelfDamage)
{
    public static DamageInfo From(object? source, Vector3 worldPosition, float amount, bool isSelfDamage) =>
        new(amount, source, worldPosition, isSelfDamage);
}

/// <summary>
/// Anything that can take damage. Deliberately one method and nothing else: no teams, no armour, no status
/// effects, no callbacks. Weapons depend only on this, so anything that implements it can be shot.
/// </summary>
public interface IDamageable
{
    /// <summary>Health remaining.</summary>
    float CurrentHealth { get; }

    /// <summary>Health at full strength.</summary>
    float MaxHealth { get; }

    /// <summary>True once health has reached zero.</summary>
    bool IsDead { get; }

    /// <summary>
    /// Applies <paramref name="damage"/>. Returns the health actually left, which is what lets an explosion
    /// report what it did to a target.
    /// </summary>
    float TakeDamage(DamageInfo damage);
}

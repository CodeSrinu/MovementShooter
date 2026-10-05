using System;

namespace MovementShooter.Combat;

/// <summary>
/// A hit-point pool, and the whole of the damage model for this milestone.
///
/// Damage is cumulative and never regenerates: health only ever goes down, and several explosions simply add
/// up. There is no armour, no resistance, no invulnerability window and no healing, because none of those are
/// designed yet and guessing at them now would only make them harder to change later.
/// </summary>
public sealed class Health : IDamageable
{
    private float _current;

    public Health(float maxHealth)
    {
        if (maxHealth <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Health must be positive.");
        }

        MaxHealth = maxHealth;
        _current = maxHealth;
    }

    public float MaxHealth { get; }

    public float CurrentHealth => _current;

    public bool IsDead => _current <= 0f;

    /// <summary>Total damage ever taken, which is what the debug readout shows.</summary>
    public float TotalDamageTaken { get; private set; }

    /// <summary>
    /// Applies damage and returns the health left.
    ///
    /// Zero and negative amounts are ignored rather than treated as healing: a caller that computes a falloff of
    /// zero at the edge of a radius should apply nothing, and silently adding health there would turn every
    /// near-miss explosion into a repair. Once dead, a target stays dead - no further damage is applied and none
    /// is reported, so a second rocket landing on a corpse cannot double-count.
    /// </summary>
    public float TakeDamage(DamageInfo damage)
    {
        if (IsDead || damage.Amount <= 0f)
        {
            return _current;
        }

        _current = MathF.Max(0f, _current - damage.Amount);
        TotalDamageTaken += damage.Amount;
        return _current;
    }

    /// <summary>Restores health to the maximum. Development-only; there is no healing in the game yet.</summary>
    public void Reset() => _current = MaxHealth;

    public override string ToString() => $"{_current:0.#}/{MaxHealth:0.#}";
}

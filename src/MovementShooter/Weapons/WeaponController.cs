using System;
using Microsoft.Xna.Framework;
using MovementShooter.Combat;
using MovementShooter.Physics;
using MovementShooter.Player;

namespace MovementShooter.Weapons;

/// <summary>
/// What the player owns: the equipped weapon, the projectile system that carries its shots, and the input that
/// drives it.
///
/// This is the only class gameplay asks to fire. It owns no weapon logic - a weapon builds its own shots, and
/// the projectile system moves them - so adding a second weapon later means holding two of these or swapping
/// the reference, not changing this file's behaviour.
/// </summary>
public sealed class WeaponController
{
    private readonly ExplosionSystem _explosions;
    private readonly ProjectileSystem _projectiles;
    private Weapon _weapon;
    private CombatTarget? _owner;

    public WeaponController(
        PhysicsWorld physics,
        ExplosionSystem explosions,
        Weapon weapon,
        CombatTarget? owner = null)
    {
        ArgumentNullException.ThrowIfNull(physics);
        _explosions = explosions ?? throw new ArgumentNullException(nameof(explosions));
        _weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
        _owner = owner;

        _projectiles = new ProjectileSystem(physics, explosions);

        // Stepping is registered with the physics world rather than driven from Update, so a projectile advances
        // in exactly the same fixed substeps as the player. Driving it from Update would make projectile travel
        // frame-rate dependent at high frame rates, where a frame may run no substep at all.
        physics.AddPreStepCallback(() => _projectiles.Step(PhysicsDefaults.FixedTimeStep));
    }

    /// <summary>The equipped weapon.</summary>
    public Weapon Weapon => _weapon;

    public string WeaponName => _weapon.Tuning.Name;

    /// <summary>Projectiles in flight right now.</summary>
    public int LiveProjectiles => _projectiles.LiveCount;

    public ProjectileSystem Projectiles => _projectiles;

    /// <summary>Who this controller's shots belong to, for self-damage recognition.</summary>
    public CombatTarget? Owner
    {
        get => _owner;
        set => _owner = value;
    }

    /// <summary>
    /// Fires the equipped weapon along a direction. Returns false when the weapon is cooling down or the
    /// direction is degenerate.
    /// </summary>
    public bool TryFire(Vector3 origin, Vector3 direction)
    {
        Shot? shot = _weapon.TryFire(new FireRequest(origin, direction, _owner));
        if (shot is null)
        {
            return false;
        }

        _projectiles.Spawn(shot.Value, _owner);
        return true;
    }

    /// <summary>Advances the weapon's cooldown. Called from the frame update, not per substep.</summary>
    public void StepCooldowns(float deltaSeconds) => _weapon.Step(deltaSeconds);

    /// <summary>Equips a different weapon, clearing the current cooldown so it is usable immediately.</summary>
    public void Equip(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        _weapon = weapon;
    }
}

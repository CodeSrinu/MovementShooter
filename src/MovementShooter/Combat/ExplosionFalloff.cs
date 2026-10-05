using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Combat;

/// <summary>
/// How an explosion's damage and impulse weaken with distance. Pure functions, no state, no engine types -
/// which is what makes the falloff testable on its own rather than only through a live explosion.
/// </summary>
public static class ExplosionFalloff
{
    /// <summary>
    /// Strength at a given distance from the centre, from 1 at the centre to 0 at the outer edge.
    ///
    /// This is the smoothstep curve <c>t * t * (3 - 2t)</c> over <c>t = 1 - distance/radius</c>. It is chosen for
    /// two reasons. It reaches exactly zero at the radius, so the affected set is a clean boundary rather than a
    /// value that merely gets small; and it is symmetric about the midpoint, so a target at half the radius gets
    /// exactly half strength - linear falloff would put most of its drop at the very edge, where the force is
    /// already weak and the variation is imperceptible, and leave the whole inner half feeling identical.
    ///
    /// Anything outside the radius returns exactly zero, and there is no negative region to leak past the edge.
    /// </summary>
    public static float Strength(float distance, float radius)
    {
        if (radius <= 0f || distance >= radius)
        {
            return 0f;
        }

        // Exactly at the centre the direction is undefined, so the strength is defined as full and the caller
        // picks the direction. Treating the centre as a special case here rather than in the caller keeps this
        // function usable by anything that only wants a number.
        float t = 1f - (distance / radius);
        return t * t * ((3f - (2f * t)));
    }

    /// <summary>
    /// Impulse direction for a blast at <paramref name="centre"/> acting on a target at
    /// <paramref name="target"/>. Points away from the centre, which is what throws things outward.
    ///
    /// A target exactly at the centre has no outward direction, so <paramref name="centreFallback"/> is used
    /// instead - world up, which is the direction a blast underfoot should push. Callers choose the fallback to
    /// match their intent; the default suits an explosion meant to throw things upward.
    /// </summary>
    public static Vector3 Direction(Vector3 centre, Vector3 target, Vector3? centreFallback = null)
    {
        Vector3 away = target - centre;
        float lengthSquared = away.LengthSquared();

        if (lengthSquared > 1e-8f)
        {
            return Vector3.Normalize(away);
        }

        return Vector3.Normalize(centreFallback ?? Vector3.Up);
    }

    /// <summary>
    /// The impulse a blast of <paramref name="maxForce"/> at <paramref name="centre"/> delivers to a body at
    /// <paramref name="target"/>, before the body's own mass is taken into account.
    ///
    /// Magnitude is force scaled by falloff, direction is radially outward. There are no special cases per
    /// target: standing in the blast and being a bystander are the same calculation, which is what makes
    /// self-damage and enemy damage identical rather than two systems that happen to agree.
    /// </summary>
    public static Vector3 Impulse(Vector3 centre, Vector3 target, float maxForce, float radius, Vector3? centreFallback = null)
    {
        float distance = Vector3.Distance(centre, target);
        float strength = Strength(distance, radius);

        if (strength <= 0f)
        {
            return Vector3.Zero;
        }

        return Direction(centre, target, centreFallback) * (maxForce * strength);
    }

    /// <summary>Damage a blast of <paramref name="maxDamage"/> deals at a distance, before any self-damage scale.</summary>
    public static float Damage(float distance, float maxDamage, float radius) =>
        maxDamage * Strength(distance, radius);
}

using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Physics;

/// <summary>
/// Conversions between the game's math types (MonoGame) and the physics engine's math types
/// (System.Numerics). Keeping them in one file makes the boundary obvious: nothing else in the codebase
/// should reference <c>System.Numerics</c> or BEPU types.
/// </summary>
internal static class Num
{
    public static System.Numerics.Vector3 ToNum(this Vector3 value) =>
        new(value.X, value.Y, value.Z);

    public static Vector3 ToGame(this System.Numerics.Vector3 value) =>
        new(value.X, value.Y, value.Z);

    public static System.Numerics.Quaternion ToNum(this Quaternion value) =>
        new(value.X, value.Y, value.Z, value.W);

    public static Quaternion ToGame(this System.Numerics.Quaternion value) =>
        new(value.X, value.Y, value.Z, value.W);
}
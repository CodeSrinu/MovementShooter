using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Map;

/// <summary>Primitive shapes the map is built from.</summary>
public enum MapBlockKind
{
    Box,
    Ramp,
    Cylinder,
}

/// <summary>
/// One piece of level geometry. A block is described once and consumed twice - by
/// <see cref="MapRenderer"/> to build meshes and by <see cref="MapColliders"/> to build colliders - so the
/// thing you see and the thing you collide with can never drift apart.
/// </summary>
/// <param name="Kind">Primitive shape.</param>
/// <param name="Size">
/// Box: full extents. Ramp: footprint on X and Z plus height on Y. Cylinder: radius on X, full height on Y.
/// </param>
/// <param name="Position">Centre of the block in world space.</param>
/// <param name="YawDegrees">Rotation around the Y axis.</param>
/// <param name="Color">Vertex colour used when rendering.</param>
public readonly record struct MapBlock(
    MapBlockKind Kind,
    Vector3 Size,
    Vector3 Position,
    float YawDegrees,
    Color Color);
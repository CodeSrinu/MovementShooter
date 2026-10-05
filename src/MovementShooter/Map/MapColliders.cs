using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Physics;

namespace MovementShooter.Map;

/// <summary>Builds static colliders for a list of <see cref="MapBlock"/>s.</summary>
public static class MapColliders
{
    /// <summary>Adds the ground plus every block in <paramref name="blocks"/> to the physics world.</summary>
    public static void Build(PhysicsWorld physics, IReadOnlyList<MapBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(blocks);

        // The ground is a slab whose top face sits at y = 0, matching the rendered plane.
        physics.AddStaticBox(new Vector3(TestArena.GroundSize, 2f, TestArena.GroundSize), new Vector3(0f, -1f, 0f));

        foreach (MapBlock block in blocks)
        {
            switch (block.Kind)
            {
                case MapBlockKind.Box:
                    physics.AddStaticBox(block.Size, block.Position, block.YawDegrees);
                    break;
                case MapBlockKind.Ramp:
                    physics.AddStaticConvexHull(RampPoints(block.Size), block.Position, block.YawDegrees);
                    break;
                case MapBlockKind.Cylinder:
                    physics.AddStaticCylinder(block.Size.X, block.Size.Y, block.Position);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(block), block.Kind, "Unknown map block kind.");
            }
        }
    }

    /// <summary>
    /// The six corners of a wedge matching <see cref="MeshBuilder.AddRamp"/>: a right triangle rising from
    /// y=0 at -Z to y=height at +Z, centred on the origin.
    /// </summary>
    public static Vector3[] RampPoints(Vector3 size)
    {
        float x = size.X * 0.5f;
        float z = size.Z * 0.5f;
        float height = size.Y;

        return new[]
        {
            new Vector3(-x, 0f, -z),
            new Vector3(x, 0f, -z),
            new Vector3(x, 0f, z),
            new Vector3(-x, 0f, z),
            new Vector3(-x, height, z),
            new Vector3(x, height, z),
        };
    }
}
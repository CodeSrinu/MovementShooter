using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using MovementShooter.Core;
using MovementShooter.Map;
using MovementShooter.Physics;

namespace MovementShooter.Diagnostics;

/// <summary>
/// Compares the geometry the renderer builds against the geometry the physics world builds, block by block.
/// A collider with no matching mesh (or a mesh somewhere else) is the "invisible geometry" failure, so this
/// is the audit that must stay green.
/// </summary>
public static class GeometryAudit
{
    private const float Tolerance = 0.02f;

    public static IReadOnlyList<string> Run()
    {
        List<string> problems = new();
        IReadOnlyList<MapBlock> blocks = TestArena.Blocks;

        using PhysicsWorld physics = new();
        MapColliders.Build(physics, blocks);

        IReadOnlyList<ColliderShape> colliders = physics.Shapes;

        // Shapes[0] is the ground slab, which MapColliders builds directly rather than from a MapBlock.
        if (colliders.Count == 0 || colliders[0].Kind != ColliderKind.Box)
        {
            problems.Add("the ground collider is missing");
            return problems;
        }

        Vector3 expectedGround = new(TestArena.GroundSize, 2f, TestArena.GroundSize);
        if (Vector3.Distance(colliders[0].BoxSize, expectedGround) > Tolerance ||
            Vector3.Distance(colliders[0].Transform.Translation, new Vector3(0f, -1f, 0f)) > Tolerance)
        {
            problems.Add($"ground collider is {Describe(colliders[0].BoxSize)} at {Describe(colliders[0].Transform.Translation)}, expected {Describe(expectedGround)} at (0, -1, 0)");
        }

        int colliderIndex = 1;

        foreach (MapBlock block in blocks)
        {
            if (colliderIndex >= colliders.Count)
            {
                problems.Add($"block {Describe(block)}: no collider was created");
                continue;
            }

            ColliderShape collider = colliders[colliderIndex++];
            CompareBlock(block, collider, problems);
        }

        if (colliderIndex != colliders.Count)
        {
            problems.Add($"{colliders.Count - colliderIndex} collider(s) exist with no matching map block");
        }

        return problems;
    }

    private static void CompareBlock(MapBlock block, ColliderShape collider, List<string> problems)
    {
        if (!KindMatches(block.Kind, collider.Kind))
        {
            problems.Add($"{Describe(block)}: collider is a {collider.Kind}, expected {block.Kind}");
            return;
        }

        switch (block.Kind)
        {
            case MapBlockKind.Box:
                CompareVector(block.Size, collider.BoxSize, $"{Describe(block)} box size", problems);
                break;

            case MapBlockKind.Cylinder:
                CompareScalar(block.Size.X, collider.Radius, $"{Describe(block)} radius", problems);
                CompareScalar(block.Size.Y, collider.Length, $"{Describe(block)} height", problems);
                break;

            case MapBlockKind.Ramp:
                CompareRamp(block, collider, problems);
                break;
        }
    }

    /// <summary>
    /// A ramp's collider is a convex hull that the engine recentres, so its stored pose is offset from the
    /// block position. What must match exactly is the world-space vertex cloud: the rendered mesh's corners
    /// and the collider's points have to be the same points in the same place.
    /// </summary>
    private static void CompareRamp(MapBlock block, ColliderShape collider, List<string> problems)
    {
        IReadOnlyList<Vector3> expected = MapColliders.RampPoints(block.Size);
        Matrix transform = MapRenderer.TransformOf(block);

        if (collider.HullPoints.Count != expected.Count)
        {
            problems.Add($"{Describe(block)}: collider hull has {collider.HullPoints.Count} points, expected {expected.Count}");
            return;
        }

        IReadOnlyList<Vector3> collided = collider.HullPoints;

        for (int i = 0; i < expected.Count; i++)
        {
            Vector3 rendered = Vector3.Transform(expected[i], transform);

            if (Vector3.Distance(rendered, collided[i]) > Tolerance)
            {
                problems.Add($"{Describe(block)}: hull point {i} rendered at {Describe(rendered)} but collides at {Describe(collided[i])}");
            }
        }
    }

    private static bool KindMatches(MapBlockKind blockKind, ColliderKind colliderKind) => blockKind switch
    {
        MapBlockKind.Box => colliderKind == ColliderKind.Box,
        MapBlockKind.Cylinder => colliderKind == ColliderKind.Cylinder,
        MapBlockKind.Ramp => colliderKind == ColliderKind.ConvexHull,
        _ => false,
    };

    private static void CompareVector(Vector3 expected, Vector3 actual, string label, List<string> problems)
    {
        if (Vector3.Distance(expected, actual) > Tolerance)
        {
            problems.Add($"{label}: expected {Describe(expected)}, collider has {Describe(actual)}");
        }
    }

    private static void CompareScalar(float expected, float actual, string label, List<string> problems)
    {
        if (Math.Abs(expected - actual) > Tolerance)
        {
            problems.Add($"{label}: expected {F(expected)}, collider has {F(actual)}");
        }
    }

    private static string Describe(MapBlock block) =>
        $"{block.Kind} size {Describe(block.Size)} at {Describe(block.Position)} yaw {F(block.YawDegrees)}";

    private static string Describe(Vector3 value) =>
        $"({F(value.X)}, {F(value.Y)}, {F(value.Z)})";

    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
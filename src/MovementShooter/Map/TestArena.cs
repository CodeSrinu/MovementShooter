using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MovementShooter.Map;

/// <summary>
/// The bootstrap test playground, described as data. Design goals:
/// <list type="bullet">
/// <item>Verticality: ground, mid platforms, high platforms.</item>
/// <item>Alternate routes: steps on one side, ramps on the other.</item>
/// <item>Rocket-jump gaps: horizontal and vertical gaps sized for a rocket launch.</item>
/// <item>Scale reference: 1-4 m cubes and a 24 m pillar.</item>
/// </list>
/// Orientation: -Z is north, +Z is south, +X is east, -X is west.
/// </summary>
public static class TestArena
{
    public const float GroundSize = 200f;
    public const float GroundCellSize = 5f;
    public const float WallHeight = 8f;

    private static readonly Color GroundLight = new(0.34f, 0.36f, 0.40f);
    private static readonly Color GroundDark = new(0.24f, 0.26f, 0.30f);
    private static readonly Color WallColor = new(0.30f, 0.33f, 0.40f);
    private static readonly Color PlatformColor = new(0.46f, 0.50f, 0.56f);
    private static readonly Color StepColor = new(0.55f, 0.44f, 0.32f);
    private static readonly Color RampColor = new(0.40f, 0.52f, 0.44f);
    private static readonly Color PillarColor = new(0.38f, 0.36f, 0.48f);
    private static readonly Color PadColor = new(1.00f, 0.55f, 0.15f);
    private static readonly Color ScaleColor = new(0.20f, 0.85f, 0.95f);

    private static IReadOnlyList<MapBlock>? _blocks;

    /// <summary>All level geometry, excluding the ground which both renderers build themselves.</summary>
    public static IReadOnlyList<MapBlock> Blocks => _blocks ??= BuildBlocks();

    private static IReadOnlyList<MapBlock> BuildBlocks()
    {
        List<MapBlock> blocks = new();

        float half = GroundSize * 0.5f;

        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(2f, WallHeight, GroundSize), new Vector3(half, WallHeight * 0.5f, 0f), 0f, WallColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(2f, WallHeight, GroundSize), new Vector3(-half, WallHeight * 0.5f, 0f), 0f, WallColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(GroundSize, WallHeight, 2f), new Vector3(0f, WallHeight * 0.5f, -half), 0f, WallColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(GroundSize, WallHeight, 2f), new Vector3(0f, WallHeight * 0.5f, half), 0f, WallColor));

        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(6f, 2f, 6f), new Vector3(12f, 1f, 0f), 0f, StepColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(6f, 4f, 6f), new Vector3(21f, 2f, 0f), 0f, StepColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(6f, 6f, 6f), new Vector3(30f, 3f, 0f), 0f, StepColor));

        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(8f, 0.6f, 8f), new Vector3(12f, 7f, -16f), 0f, PlatformColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(7f, 0.6f, 7f), new Vector3(26f, 11f, -22f), 0f, PlatformColor));
        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(6f, 0.6f, 6f), new Vector3(-18f, 15f, 14f), 0f, PlatformColor));

        blocks.Add(new MapBlock(MapBlockKind.Ramp, new Vector3(8f, 4f, 16f), new Vector3(0f, 0f, 22f), 180f, RampColor));
        blocks.Add(new MapBlock(MapBlockKind.Ramp, new Vector3(8f, 4f, 16f), new Vector3(-26f, 0f, 0f), 180f, RampColor));
        blocks.Add(new MapBlock(MapBlockKind.Ramp, new Vector3(8f, 4f, 16f), new Vector3(34f, 0f, 26f), 0f, RampColor));

        float[] pillarHeights = { 5f, 7f, 9f, 11f, 13f, 24f };
        const float pillarRingRadius = 42f;
        for (int i = 0; i < pillarHeights.Length; i++)
        {
            float angle = MathHelper.TwoPi * i / pillarHeights.Length;
            Vector3 position = new(MathF.Cos(angle) * pillarRingRadius, pillarHeights[i] * 0.5f, MathF.Sin(angle) * pillarRingRadius);
            blocks.Add(new MapBlock(MapBlockKind.Cylinder, new Vector3(1.4f, pillarHeights[i], 0f), position, 0f, PillarColor));
        }

        blocks.Add(new MapBlock(MapBlockKind.Cylinder, new Vector3(4f, 0.25f, 0f), new Vector3(0f, 0.125f, 34f), 0f, PadColor));
        blocks.Add(new MapBlock(MapBlockKind.Cylinder, new Vector3(4f, 0.25f, 0f), new Vector3(-14f, 0.125f, -34f), 0f, PadColor));

        for (int i = 1; i <= 4; i++)
        {
            blocks.Add(new MapBlock(MapBlockKind.Box, Vector3.One * i, new Vector3(-6f + (i * 2f), i * 0.5f, -8f), 0f, ScaleColor));
        }

        blocks.Add(new MapBlock(MapBlockKind.Box, new Vector3(2f, 4f, 2f), new Vector3(6f, 2f, -8f), 0f, ScaleColor));

        return blocks;
    }
}
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Graphics;

namespace MovementShooter.Map;

/// <summary>Builds GPU geometry for a list of <see cref="MapBlock"/>s.</summary>
public static class MapRenderer
{
    /// <summary>A renderable together with the block it came from, for geometry audits.</summary>
    public readonly record struct BuiltRenderable(MapBlock Block, Renderable Renderable, Matrix Transform);

    /// <summary>Adds a checkerboard ground plus every block in <paramref name="blocks"/> to the world.</summary>
    public static void Build(World world, IReadOnlyList<MapBlock> blocks) =>
        BuildWithBlocks(world, blocks);

    /// <summary>
    /// Same as <see cref="Build"/> but also returns what was built, so tests can compare the rendered
    /// geometry against the collision geometry block by block.
    /// </summary>
    public static IReadOnlyList<BuiltRenderable> BuildWithBlocks(World world, IReadOnlyList<MapBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(blocks);

        SurfaceMaterial lit = world.Track(SurfaceMaterial.CreateLit(world.Device, Color.White));
        AddGround(world, lit);

        List<BuiltRenderable> built = new(blocks.Count);

        foreach (MapBlock block in blocks)
        {
            Matrix transform = TransformOf(block);
            MeshData mesh = MeshFor(block);

            built.Add(new BuiltRenderable(block, world.Add(mesh, transform, lit), transform));
        }

        return built;
    }

    private static MeshData MeshFor(MapBlock block) => block.Kind switch
    {
        MapBlockKind.Box => new MeshBuilder().AddBox(block.Size, block.Color).Build(),
        MapBlockKind.Ramp => new MeshBuilder().AddRamp(new Vector2(block.Size.X, block.Size.Z), block.Size.Y, block.Color).Build(),
        MapBlockKind.Cylinder => new MeshBuilder().AddCylinder(block.Size.X, block.Size.Y, block.Color, segments: 24).Build(),
        _ => throw new ArgumentOutOfRangeException(nameof(block), block.Kind, "Unknown map block kind."),
    };

    private static void AddGround(World world, SurfaceMaterial material)
    {
        const float size = TestArena.GroundSize;
        const float cell = TestArena.GroundCellSize;
        int cells = (int)(size / cell);
        float step = size / cells;
        float half = size * 0.5f;

        // A checkerboard makes speed, distance and camera height easy to judge, and needs per-cell colours,
        // so it is built here rather than through MeshBuilder.
        MeshBuilder builder = new();
        Color light = new(0.34f, 0.36f, 0.40f);
        Color dark = new(0.24f, 0.26f, 0.30f);

        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                float x0 = -half + (x * step);
                float z0 = -half + (z * step);

                builder.AddQuad(
                    new Vector3(x0, 0f, z0),
                    new Vector3(x0, 0f, z0 + step),
                    new Vector3(x0 + step, 0f, z0 + step),
                    new Vector3(x0 + step, 0f, z0),
                    (x + z) % 2 == 0 ? light : dark);
            }
        }

        world.Add(builder.Build(), Matrix.Identity, material);
    }

private static void AddBox(World world, MapBlock block, SurfaceMaterial material)
    {
        MeshData mesh = new MeshBuilder().AddBox(block.Size, block.Color).Build();
        world.Add(mesh, TransformOf(block), material);
    }

/// <summary>
    /// Places a block. Composition order matters: with MonoGame's row-vector convention <c>A * B</c>
    /// applies A first, so this must be rotation-then-translation. The reverse order rotates the
    /// translation too, which threw yawed blocks (the ramps) tens of metres away from their colliders.
    /// </summary>
    public static Matrix TransformOf(MapBlock block) =>
        Matrix.CreateRotationY(MathHelper.ToRadians(block.YawDegrees)) * Matrix.CreateTranslation(block.Position);
}
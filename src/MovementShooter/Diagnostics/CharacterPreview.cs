using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Character;
using MovementShooter.Core;
using MovementShooter.Graphics;

namespace MovementShooter.Diagnostics;

/// <summary>
/// Renders the character offscreen from known angles and poses, into one contact sheet.
///
/// Necessary rather than convenient: the game is first-person and the character's
/// origin is the player's own position, so the local body is never in frame during
/// normal play. Without this there is no way to actually look at the mesh, and
/// "correct materials, correct skinning, correct pose" would be claims rather than
/// observations.
///
/// Lives entirely in Diagnostics and is only reachable behind an explicit command
/// line flag, so it cannot affect a normal run.
/// </summary>
public static class CharacterPreview
{
    private const int TileWidth = 320;
    private const int TileHeight = 400;
    private const int Columns = 4;

    private sealed record Shot(string Label, int Clip, float Time, float AzimuthDegrees, float ElevationDegrees);

    private static readonly Shot[] Shots =
    {
        // Turntable on the idle pose: catches mirroring, scale and the palette.
        new("front", 0, 0f, 0f, 6f),
        new("three-quarter", 0, 0f, 38f, 10f),
        new("side", 0, 0f, 90f, 6f),
        new("back", 0, 0f, 180f, 8f),

        // One representative pose per gameplay clip.
        new("run", 1, 0.27f, 62f, 8f),
        new("jump", 2, 0.20f, 62f, 8f),
        new("fall", 3, 0.40f, 62f, 8f),
        new("land", 4, 0.03f, 62f, 8f),
        new("slide", 5, 0.53f, 62f, 8f),
        new("dash", 6, 0.23f, 62f, 8f),
        new("fire", 7, 0.10f, 62f, 8f),

        // Standing at distance, for scale against the arena floor grid.
        new("scale-ref", 0, 0f, 24f, 3f),

        // Pure bind pose, no clip sampled at all. Bisects "the render path is wrong"
        // from "the clip sampling is wrong" - if this tile is upright and the others are
        // not, the skeleton and the buffers are fine and the pose data is the problem.
        new("bind-pose", -1, 0f, 24f, 8f),
    };

    /// <summary>
    /// Renders every shot and writes a single contact sheet. Returns the file path.
    /// </summary>
public static string Render(GraphicsDevice device, CharacterAsset asset, CharacterTuning tuning,
        string outputDirectory, float capsuleHeight, WeaponAttachment? weapon = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(tuning);

        Directory.CreateDirectory(outputDirectory);

        int rows = (Shots.Length + Columns - 1) / Columns;
        int sheetWidth = TileWidth * Columns;
        int sheetHeight = TileHeight * rows;

        using RenderTarget2D sheetTarget = new(device, sheetWidth, sheetHeight, false,
            SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);
        using RenderTarget2D tileTarget = new(device, TileWidth, TileHeight, false,
            SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        using RenderTarget2D depthTarget = new(device, sheetWidth, sheetHeight, false,
            SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);

        // A small three-point rig and a floor, so the sheet shows the character lit and
        // grounded rather than floating in a void.
        RenderStateSetUp(device);
        MaterialAndLights lights = MaterialAndLights.Create(device);

        CharacterSkeleton skeleton = new(asset);
        CharacterSkeleton scratch = new(asset);
        CharacterSkin skin = new(asset);
        CharacterAnimator animator = new(asset, skeleton, scratch);
        CharacterRenderer renderer = new(device, asset, skin);

        // A 2 m tile under the character gives an unambiguous scale reference: the
        // character is 1.795 m, so it should stand just under one tile.
        VertexPositionColorNormal[] floorMesh = BuildFloorTile(2f);
        GpuMesh floor = GpuMesh.Create(device, new MeshData(floorMesh,
            new[] { 0, 1, 2, 0, 2, 3 }));

        Color[] pixels = new Color[sheetWidth * sheetHeight];
        List<string> labels = new();

        for (int index = 0; index < Shots.Length; index++)
        {
            Shot shot = Shots[index];
            labels.Add(shot.Label);

            // Hold the requested clip at the requested time by driving the animator's
            // own sample path, then skinning it.
            PoseAt(asset, animator, skeleton, shot.Clip, shot.Time);
            skin.Skin(skeleton);
            renderer.Update(skin);

device.SetRenderTarget(tileTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(24, 28, 38), 1f, 0);

            float azimuth = MathHelper.ToRadians(shot.AzimuthDegrees);
            float elevation = MathHelper.ToRadians(shot.ElevationDegrees);
            float distance = 2.45f;

            Vector3 target = new(0f, capsuleHeight * 0.5f, 0f);
            Vector3 eye = target + new Vector3(
                MathF.Sin(azimuth) * MathF.Cos(elevation) * distance,
                MathF.Sin(elevation) * distance,
                MathF.Cos(azimuth) * MathF.Cos(elevation) * distance);

            Matrix view = Matrix.CreateLookAt(eye, target, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(42f), (float)TileWidth / TileHeight, 0.05f, 60f);

            lights.Apply(view, projection);
            lights.Draw(floor, Matrix.Identity, view, projection);

// The character's origin is at its feet, matching the floor tile.
            renderer.Draw(Matrix.Identity, view, projection);

            // The launcher, drawn immediately after the character so it depth-tests against
            // the body it hangs from. Its transform comes from the character's WeaponSocket,
            // posed on this same skeleton - the preview drives the skeleton directly rather
            // than a CharacterBody, so the socket world matrix is composed here from the
            // posed skeleton and the identity character placement.
            if (weapon is not null)
            {
                int socket = asset.IndexOfSocket(WeaponAttachment.WeaponSocketName);
                if (socket >= 0)
                {
                    weapon.DrawAt(skeleton.SocketWorld(socket), view, projection);
                }
            }

            device.SetRenderTarget(null);

            // Read the tile back before the next shot overwrites it. DesktopGL queues
            // draw commands, so this drains the target rather than racing ahead of the
            // GPU; the copy into the sheet has to happen while the pixels are here.
            Color[] tile = new Color[TileWidth * TileHeight];
            tileTarget.GetData(tile);
            Blit(tile, pixels, sheetWidth, sheetHeight, index);

            // Each tile is also written on its own. A contact sheet hides which shot is
            // wrong, and when a readback looks stale this is what settles it.
            Core.PngWriter.Save(Path.Combine(outputDirectory,
                string.Format(CultureInfo.InvariantCulture, "tile-{0:00}-{1}.png", index, shot.Label)),
                TileWidth, TileHeight, ToRgba(tile));
        }

        // PngWriter rather than RenderTarget2D.Save: the project already owns a
        // BCL-only encoder, and using it keeps this diagnostic free of any
        // content-pipeline dependency.
        string path = Path.Combine(outputDirectory, "character-preview.png");
        Core.PngWriter.Save(path, sheetWidth, sheetHeight, ToRgba(pixels));

        renderer.Dispose();
        floor.Dispose();
        lights.Dispose();

        return string.Join(", ", labels);
    }

    /// <summary>
    /// Samples a specific clip at a specific time, bypassing the state machine.
    ///
    /// The animator only exposes "advance by delta", which cannot jump to an
    /// arbitrary point in an arbitrary clip. Rather than widen the animator's
    /// public surface for a diagnostic, this reaches the clip through the same
    /// skeleton API the animator uses.
    /// </summary>
    private static void PoseAt(CharacterAsset asset, CharacterAnimator animator, CharacterSkeleton skeleton,
        int clipIndex, float time)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(skeleton);

        if ((uint)clipIndex >= (uint)asset.Clips.Length && clipIndex >= 0)
        {
            skeleton.Reset();
            return;
        }

        if (clipIndex < 0)
        {
            skeleton.Reset();
            return;
        }

        animator.PoseClip(clipIndex, time, skeleton);
    }

    /// <summary>Packs pixels to the RGBA byte layout PngWriter expects.</summary>
    private static byte[] ToRgba(Color[] pixels)
    {
        byte[] rgba = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            rgba[(i * 4) + 0] = pixels[i].R;
            rgba[(i * 4) + 1] = pixels[i].G;
            rgba[(i * 4) + 2] = pixels[i].B;
            rgba[(i * 4) + 3] = 255;
        }

        return rgba;
    }

    private static void Blit(Color[] tile, Color[] sheet, int sheetWidth, int sheetHeight, int index)
    {
        int column = index % Columns;
        int rowFromTop = index / Columns;

        // Rows are counted from the bottom of the array, because that is the end
        // PngWriter writes first - writing shot 0 to array row 0 puts it at the
        // bottom of the image and reverses the sheet's reading order.
        int rowFromBottom = (sheetHeight / TileHeight) - 1 - rowFromTop;

        for (int y = 0; y < TileHeight; y++)
        {
            int sheetRow = (rowFromBottom * TileHeight) + y;
            if (sheetRow >= sheetHeight)
            {
                break;
            }

            for (int x = 0; x < TileWidth; x++)
            {
                sheet[(sheetRow * sheetWidth) + (column * TileWidth) + x] = tile[(y * TileWidth) + x];
            }
        }
    }

    private static void RenderStateSetUp(GraphicsDevice device)
    {
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = Map.World.FrontFaceState;
    }

    /// <summary>A single 2 m floor quad, lying in the XZ plane at y = 0.</summary>
    private static VertexPositionColorNormal[] BuildFloorTile(float size)
    {
        float half = size * 0.5f;
        Color colour = new(46, 50, 58);
        return new[]
        {
            new VertexPositionColorNormal(new Vector3(-half, 0f, -half), colour, Vector3.Up),
            new VertexPositionColorNormal(new Vector3(-half, 0f, half), colour, Vector3.Up),
            new VertexPositionColorNormal(new Vector3(half, 0f, half), colour, Vector3.Up),
            new VertexPositionColorNormal(new Vector3(half, 0f, -half), colour, Vector3.Up),
        };
    }

    /// <summary>
    /// A minimal lit rig for the preview sheet. Deliberately separate from the game's
    /// arena lighting so the sheet is reproducible regardless of where the scene lights are.
    /// </summary>
    private sealed class MaterialAndLights : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;

        private MaterialAndLights(GraphicsDevice device, BasicEffect effect)
        {
            _device = device;
            _effect = effect;
        }

        public static MaterialAndLights Create(GraphicsDevice device)
        {
            BasicEffect effect = new(device)
            {
                LightingEnabled = true,
                TextureEnabled = false,
                VertexColorEnabled = true,
                FogEnabled = false,
                AmbientLightColor = new Vector3(0.45f, 0.47f, 0.52f),
            };

            effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -0.8f, -0.4f));
            effect.DirectionalLight0.DiffuseColor = new Vector3(1f, 0.97f, 0.9f);
            effect.DirectionalLight0.SpecularColor = new Vector3(0.08f, 0.08f, 0.09f);
            effect.SpecularPower = 24f;

            return new MaterialAndLights(device, effect);
        }

        public void Apply(Matrix view, Matrix projection)
        {
            _effect.View = view;
            _effect.Projection = projection;
        }

        public void Draw(GpuMesh mesh, Matrix world, Matrix view, Matrix projection)
        {
            _effect.World = world;
            _effect.DiffuseColor = Vector3.One;
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
            }

            mesh.Draw(_device);
        }

        public void Dispose() => _effect.Dispose();
    }
}

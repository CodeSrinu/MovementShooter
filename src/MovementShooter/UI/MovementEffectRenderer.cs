using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.UI;

/// <summary>
/// Draws the quads produced by <see cref="MovementEffects"/>.
///
/// One dynamic vertex buffer, rewritten each frame from the live list. That is the smallest thing that can work:
/// the effects are at most a few dozen quads, they never outlive a fraction of a second, and a dynamic buffer
/// avoids both a per-frame allocation and a draw call per streak.
/// </summary>
/// <remarks>
/// Unlit and alpha blended. A streak is a flash of light at the edge of vision, so lighting it would only make
/// it darker in shadow; and depth testing stays on, so an effect behind geometry is correctly hidden rather
/// than drawing through walls.
/// </remarks>
public sealed class MovementEffectRenderer : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly MovementEffects _effects;
    private readonly BasicEffect _effect;
    private readonly List<FeedbackQuad> _quads = new();
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[MovementFeedbackSettingsDefaults.MaxQuads * 4];
    private readonly int[] _indices = BuildQuadIndices(MovementFeedbackSettingsDefaults.MaxQuads);

    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private bool _disposed;

    public MovementEffectRenderer(GraphicsDevice device, MovementEffects effects)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _effects = effects ?? throw new ArgumentNullException(nameof(effects));

        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true,
            FogEnabled = false,
            Alpha = 1f,
        };

        // Sized from a fixed ceiling rather than from the current burst, so the buffers never change size and
        // can be created once here rather than reallocated whenever a cue fires.
        _vertexBuffer = new VertexBuffer(
            device,
            VertexPositionColor.VertexDeclaration,
            _vertices.Length,
            BufferUsage.WriteOnly);

        _indexBuffer = new IndexBuffer(
            device,
            IndexElementSize.SixteenBits,
            _indices.Length,
            BufferUsage.WriteOnly);
        _indexBuffer.SetData(_indices);
    }

    private static int[] BuildQuadIndices(int quadCount)
    {
        int[] indices = new int[quadCount * 6];
        for (int quad = 0; quad < quadCount; quad++)
        {
            int vertex = quad * 4;
            int index = quad * 6;

            // Two triangles, counter-clockwise as seen from the front.
            indices[index + 0] = vertex + 0;
            indices[index + 1] = vertex + 1;
            indices[index + 2] = vertex + 2;
            indices[index + 3] = vertex + 2;
            indices[index + 4] = vertex + 3;
            indices[index + 5] = vertex + 0;
        }

        return indices;
    }

    /// <summary>Draws every live effect. A no-op when nothing is alive, which is nearly every frame.</summary>
    public void Draw(Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MovementEffectRenderer));
        }

        int count = _effects.CollectQuads(view, _effects.Settings.PeakAlpha, _quads);
        if (count == 0)
        {
            return;
        }

        count = Math.Min(count, MovementFeedbackSettingsDefaults.MaxQuads);

        for (int i = 0; i < count; i++)
        {
            WriteQuad(_quads[i], i * 4);
        }

        _vertexBuffer!.SetData(_vertices, 0, count * 4);

        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = Map.World.FrontFaceState;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;
        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count * 6);
    }

    /// <summary>
    /// Writes one camera-facing quad as four corner vertices. The corners are built from the length and width
    /// axes directly, so the quad is a flat rectangle in world space rather than a billboard that has to be
    /// re-derived at draw time.
    /// </summary>
    private void WriteQuad(in FeedbackQuad quad, int offset)
    {
        Vector3 along = quad.Along * quad.HalfLength;
        Vector3 across = quad.Across * quad.HalfWidth;

        Vector3 centre = quad.Centre;

        // Counter-clockwise from the front face the width axis produces, matching the world's winding
        // convention: see World.FrontFaceState.
        _vertices[offset + 0] = new VertexPositionColor(centre - along - across, quad.Color);
        _vertices[offset + 1] = new VertexPositionColor(centre + along - across, quad.Color);
        _vertices[offset + 2] = new VertexPositionColor(centre + along + across, quad.Color);
        _vertices[offset + 3] = new VertexPositionColor(centre - along + across, quad.Color);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _effect.Dispose();
    }
}

/// <summary>Fixed ceilings the effect buffers are sized from, so their size is constant.</summary>
internal static class MovementFeedbackSettingsDefaults
{
    /// <summary>
    /// Hard cap on live quads. Comfortably above the largest burst the settings can produce (a slide and a dash
    /// together), so the pool is never the reason a cue fails to appear.
    /// </summary>
    public const int MaxQuads = 48;
}

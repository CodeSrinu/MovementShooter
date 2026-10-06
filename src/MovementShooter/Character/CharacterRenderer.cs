using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Graphics;
using MovementShooter.Map;

namespace MovementShooter.Character;

/// <summary>
/// GPU residency and draw for one skinned character.
///
/// The vertex buffer is dynamic because the mesh is re-skinning every frame; everything
/// else - index buffer, material, submesh ranges - is created once. The character reuses
/// <see cref="VertexPositionColorNormal"/> and <see cref="SurfaceMaterial"/>, so it needs no
/// vertex declaration, no shader and no texture, and it lights exactly like the arena does.
/// </summary>
public sealed class CharacterRenderer : IDisposable
{
    private readonly CharacterAsset _asset;
    private readonly GraphicsDevice _device;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly SurfaceMaterial _material;
    private readonly bool _wideIndices;
    private bool _disposed;

    public CharacterRenderer(GraphicsDevice device, CharacterAsset asset, CharacterSkin skin)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(skin);

        _asset = asset;
        _device = device;

        // Seed with the bind pose so the very first frame draws a sane character even if
        // the animation has not ticked yet.
        VertexPositionColorNormal[] seed = new VertexPositionColorNormal[asset.VertexCount];
        skin.WriteBindPose(seed);

        // WriteOnly + a per-frame SetData, matching UI/MovementEffectRenderer, which
        // also rewrites its buffer every frame. BufferUsage.Dynamic is not offered by
        // this MonoGame build.
        _vertexBuffer = new VertexBuffer(device, VertexPositionColorNormal.VertexDeclaration,
            asset.VertexCount, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(seed);

        _wideIndices = asset.VertexCount > ushort.MaxValue;
        IndexElementSize elementSize = _wideIndices ? IndexElementSize.ThirtyTwoBits : IndexElementSize.SixteenBits;
        _indexBuffer = new IndexBuffer(device, elementSize, asset.Indices.Length, BufferUsage.WriteOnly);

        if (_wideIndices)
        {
            _indexBuffer.SetData(asset.Indices);
        }
        else
        {
            ushort[] narrow = new ushort[asset.Indices.Length];
            for (int i = 0; i < narrow.Length; i++)
            {
                narrow[i] = (ushort)asset.Indices[i];
            }

            _indexBuffer.SetData(narrow);
        }

        // White, with vertex colour enabled: the four authored material colours are baked
        // into the vertices by the converter, so one material serves every submesh.
        _material = SurfaceMaterial.CreateLit(device, Color.White);
    }

    /// <summary>Number of draw calls per frame, one per material.</summary>
    public int SubmeshCount => _asset.Submeshes.Length;

    /// <summary>Uploads a freshly skinned pose.</summary>
    public void Update(CharacterSkin skin)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CharacterRenderer));
        }

        ArgumentNullException.ThrowIfNull(skin);

        // Whole-buffer upload. The skin array is always exactly VertexCount elements and the
        // buffer is sized to match, so the plain overload is correct here. Passing an explicit
        // byte count instead does not agree with MonoGame's packed 28-byte stride for this
        // vertex type and throws on a size mismatch.
        _vertexBuffer.SetData(skin.Vertices);
    }

    /// <summary>Draws the character. <paramref name="world"/> places it; the pose is already in the buffer.</summary>
    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CharacterRenderer));
        }

        // Opaque, depth-tested, in the same state the arena draws with. The character is
        // self-shadowing-free and writes depth so later alpha effects sort against it.
        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = World.FrontFaceState;
        _device.BlendState = BlendState.Opaque;

        _material.Apply(world, view, projection);
        _material.ApplyPasses();

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        foreach (CharacterSubmesh submesh in _asset.Submeshes)
        {
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList,
                baseVertex: 0, submesh.FirstIndex, submesh.IndexCount / 3);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }
}

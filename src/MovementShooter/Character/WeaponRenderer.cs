using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Graphics;
using MovementShooter.Map;

namespace MovementShooter.Character;

/// <summary>
    /// GPU residency and draw for one rigid weapon.
    ///
    /// The weapon is rigid, so its vertex buffer is uploaded once at construction and never
    /// touched again - the opposite of <see cref="CharacterRenderer"/>, which rewrites its
    /// buffer every frame. It reuses the same <see cref="VertexPositionColorNormal"/> format
    /// and <see cref="SurfaceMaterial"/> as everything else, so it needs no vertex
    /// declaration, no shader and no texture, and it lights exactly like the arena and the
    /// character do.
    ///
    /// <para>
    /// Shared by both viewpoints. The first-person viewmodel draws through this class too, which
    /// is what makes the same asset read as the same weapon when it moves from a character's hand
    /// to the player's own.
    /// </para>
    /// </summary>
public sealed class WeaponRenderer : IDisposable
{
    private readonly WeaponAsset _asset;
    private readonly GraphicsDevice _device;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly SurfaceMaterial _material;
    private bool _disposed;

    public WeaponRenderer(GraphicsDevice device, WeaponAsset asset)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(asset);

        _asset = asset;
        _device = device;

        VertexPositionColorNormal[] vertices = new VertexPositionColorNormal[asset.VertexCount];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new VertexPositionColorNormal(
                asset.Positions[i], asset.Colors[i], asset.Normals[i]);
        }

        // WriteOnly, uploaded once. The buffer is never rewritten, so this is not the
        // per-frame path CharacterRenderer uses.
        _vertexBuffer = new VertexBuffer(device, VertexPositionColorNormal.VertexDeclaration,
            asset.VertexCount, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(vertices);

        bool wide = asset.VertexCount > ushort.MaxValue;
        IndexElementSize elementSize = wide ? IndexElementSize.ThirtyTwoBits : IndexElementSize.SixteenBits;
        _indexBuffer = new IndexBuffer(device, elementSize, asset.Indices.Length, BufferUsage.WriteOnly);

        if (wide)
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

        // White with vertex colour enabled: the four authored material colours are baked
        // into the vertices by the converter, so one material serves every submesh.
        _material = SurfaceMaterial.CreateLit(device, Color.White);
    }

    /// <summary>Number of draw calls per frame, one per material.</summary>
    public int SubmeshCount => _asset.Submeshes.Length;

    /// <summary>The mesh this renderer draws, for diagnostics and checks.</summary>
    public WeaponAsset Asset => _asset;

    /// <summary>
    /// Draws the weapon at <paramref name="world"/>.
    ///
    /// The weapon's own space has its origin at the grip pointing along +Z, which is the
    /// same convention as the character's rig, so this matrix is the socket's world
    /// transform with nothing applied on top. That is the whole attachment mechanism: no
    /// corrective rotation, no hand-tuned offset that has to be re-tuned when the socket
    /// moves.
    /// </summary>
    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WeaponRenderer));
        }

        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = World.FrontFaceState;
        _device.BlendState = BlendState.Opaque;

        _material.Apply(world, view, projection);
        _material.ApplyPasses();

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        foreach (WeaponSubmesh submesh in _asset.Submeshes)
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
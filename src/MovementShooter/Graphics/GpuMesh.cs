using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Graphics;

/// <summary>
/// GPU-resident geometry: the vertex/index buffers for one <see cref="MeshData"/> plus a draw call.
/// Buffers are created once and reused every frame.
/// </summary>
public sealed class GpuMesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private bool _disposed;

    private GpuMesh(VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int triangleCount)
    {
        _vertexBuffer = vertexBuffer;
        _indexBuffer = indexBuffer;
        TriangleCount = triangleCount;
    }

    public int TriangleCount { get; }

    public static GpuMesh Create(GraphicsDevice device, MeshData data)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(data);

        VertexBuffer vertexBuffer = new(device, VertexPositionColorNormal.VertexDeclaration, data.Vertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(data.Vertices);

        // Prefer 16-bit indices to halve bandwidth; fall back to 32-bit for huge meshes.
        bool useShortIndices = data.Vertices.Length <= ushort.MaxValue;
        IndexElementSize elementSize = useShortIndices ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits;

        IndexBuffer indexBuffer = new(device, elementSize, data.Indices.Length, BufferUsage.WriteOnly);
        if (useShortIndices)
        {
            ushort[] shortIndices = new ushort[data.Indices.Length];
            for (int i = 0; i < data.Indices.Length; i++)
            {
                shortIndices[i] = (ushort)data.Indices[i];
            }

            indexBuffer.SetData(shortIndices);
        }
        else
        {
            indexBuffer.SetData(data.Indices);
        }

        return new GpuMesh(vertexBuffer, indexBuffer, data.TriangleCount);
    }

    public void Draw(GraphicsDevice device)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GpuMesh));
        }

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;
        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TriangleCount);
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
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Graphics;

/// <summary>
/// CPU-side geometry: interleaved vertices plus indices. Created by <see cref="MeshBuilder"/> and
/// uploaded to the GPU by <see cref="GpuMesh"/>.
/// </summary>
public sealed class MeshData
{
    public MeshData(VertexPositionColorNormal[] vertices, int[] indices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));

        if (vertices.Length == 0 || indices.Length == 0)
        {
            throw new ArgumentException("A mesh needs at least one vertex and one index.");
        }

        if (indices.Length % 3 != 0)
        {
            throw new ArgumentException("Triangle indices must come in groups of three.");
        }
    }

    public VertexPositionColorNormal[] Vertices { get; }

    public int[] Indices { get; }

    public int TriangleCount => Indices.Length / 3;
}
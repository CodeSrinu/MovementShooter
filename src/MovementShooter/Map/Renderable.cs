using System;
using Microsoft.Xna.Framework;
using MovementShooter.Graphics;

namespace MovementShooter.Map;

/// <summary>One drawable piece of world geometry: a mesh, a world transform and a material.</summary>
public sealed class Renderable : IDisposable
{
    private bool _disposed;

    public Renderable(GpuMesh mesh, Matrix transform, SurfaceMaterial material)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Transform = transform;
        Material = material ?? throw new ArgumentNullException(nameof(material));
    }

    public GpuMesh Mesh { get; }

    /// <summary>Model matrix for this piece of geometry. Moving objects update this; the map does not.</summary>
    public Matrix Transform { get; }

    public SurfaceMaterial Material { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Mesh.Dispose();
    }
}
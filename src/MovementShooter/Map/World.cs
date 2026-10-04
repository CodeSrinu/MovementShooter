using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Graphics;

namespace MovementShooter.Map;

/// <summary>
/// Owns all world geometry and materials and draws them. Deliberately simple: a flat list of
/// renderables. Batching, spatial partitioning and frustum culling arrive with the physics milestone,
/// when there is a real reason for them.
/// </summary>
public sealed class World : IDisposable
{
    /// <summary>
    /// The mesh builders emit mathematically counter-clockwise winding (the right-handed convention),
    /// while MonoGame's default treats clockwise triangles as front-facing in its left-handed space.
    /// Culling clockwise therefore keeps the faces the builders intend to be visible. Do not switch this
    /// back to <see cref="RasterizerState.CullCounterClockwise"/> without flipping the builders.
    /// </summary>
    public static readonly RasterizerState FrontFaceState = RasterizerState.CullClockwise;

    private readonly List<Renderable> _renderables = new();
    private readonly List<SurfaceMaterial> _materials = new();
    private bool _disposed;

    public World(GraphicsDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public GraphicsDevice Device { get; }

    public int RenderableCount => _renderables.Count;

    /// <summary>Uploads <paramref name="meshData"/> to the GPU. Reuse the result to place the mesh twice.</summary>
    public GpuMesh CreateMesh(MeshData meshData)
    {
        ArgumentNullException.ThrowIfNull(meshData);
        return GpuMesh.Create(Device, meshData);
    }

    /// <summary>Registers a mesh built from <paramref name="meshData"/> and gives it its own GPU buffers.</summary>
    public Renderable Add(MeshData meshData, Matrix transform, SurfaceMaterial material)
    {
        ArgumentNullException.ThrowIfNull(meshData);
        ArgumentNullException.ThrowIfNull(material);
        return Register(CreateMesh(meshData), transform, material);
    }

    /// <summary>Places an existing mesh. Call more than once to share GPU buffers between placements.</summary>
    public Renderable Add(GpuMesh mesh, Matrix transform, SurfaceMaterial material)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(material);
        return Register(mesh, transform, material);
    }

    /// <summary>Registers a material so the world disposes it on shutdown.</summary>
    public SurfaceMaterial Track(SurfaceMaterial material)
    {
        _materials.Add(material);
        return material;
    }

    public void Draw(Matrix view, Matrix projection)
    {
        Device.DepthStencilState = DepthStencilState.Default;
        Device.RasterizerState = FrontFaceState;
        Device.BlendState = BlendState.Opaque;

        foreach (Renderable renderable in _renderables)
        {
            renderable.Material.Apply(renderable.Transform, view, projection);
            renderable.Material.Draw(renderable.Mesh);
        }
    }

    private Renderable Register(GpuMesh mesh, Matrix transform, SurfaceMaterial material)
    {
        Renderable renderable = new(mesh, transform, material);
        _renderables.Add(renderable);
        return renderable;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (Renderable renderable in _renderables)
        {
            renderable.Dispose();
        }

        _renderables.Clear();

        foreach (SurfaceMaterial material in _materials)
        {
            material.Dispose();
        }

        _materials.Clear();
    }
}
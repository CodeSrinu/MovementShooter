using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Graphics;

/// <summary>
/// Thin wrapper over MonoGame's built-in <see cref="BasicEffect"/> so the rest of the game never
/// touches effect parameters directly. Materials are created once and shared between renderables;
/// per-object colour comes from the mesh's vertex colours.
/// </summary>
public sealed class SurfaceMaterial : IDisposable
{
    private static readonly Vector3 DefaultLightDirection = new(-0.45f, -0.8f, -0.4f);

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private bool _disposed;

    private SurfaceMaterial(GraphicsDevice device, Color diffuse, bool lighting)
    {
        _device = device;
        _effect = new BasicEffect(device)
        {
            DiffuseColor = diffuse.ToVector3(),
            AmbientLightColor = new Vector3(0.5f, 0.52f, 0.58f),
            Alpha = 1f,
            LightingEnabled = lighting,
            TextureEnabled = false,
            VertexColorEnabled = true,
            FogEnabled = false,
        };

        if (lighting)
        {
            _effect.DirectionalLight0.Direction = Vector3.Normalize(DefaultLightDirection);
            _effect.DirectionalLight0.DiffuseColor = new Vector3(1f, 0.97f, 0.9f);
            _effect.DirectionalLight0.SpecularColor = new Vector3(0.06f, 0.06f, 0.07f);
            _effect.SpecularPower = 32f;
        }
    }

    /// <summary>Lambert + specular material for world geometry.</summary>
    public static SurfaceMaterial CreateLit(GraphicsDevice device, Color diffuse) =>
        new(device, diffuse, lighting: true);

    public void Apply(Matrix world, Matrix view, Matrix projection)
    {
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
    }

    public void Draw(GpuMesh mesh)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SurfaceMaterial));
        }

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }

        mesh.Draw(_device);
    }

    /// <summary>
    /// Applies every technique pass without drawing, for callers that own their own
    /// geometry and buffers - the skinned character draws indexed submesh ranges out of
    /// a dynamic vertex buffer, which <see cref="Draw"/> cannot express.
    ///
    /// Added so the character lights from the same single rig as the arena instead of a
    /// fourth copy of the light setup in a renderer.
    /// </summary>
    public void ApplyPasses()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SurfaceMaterial));
        }

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _effect.Dispose();
    }
}
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Graphics;
using MovementShooter.Map;
using MovementShooter.Weapons;

namespace MovementShooter.UI;

/// <summary>
/// Renders the first-person weapon: a procedural rocket launcher, its muzzle flash, and its recoil animation.
///
/// <para>
/// Drawn in a <b>separate pass from the world</b>, after the depth buffer has been cleared. Two reasons, both
/// load-bearing:
/// </para>
/// <list type="number">
/// <item>Depth. With the world's depth still in place, standing against a wall would clip the launcher into it,
/// because the weapon is held at a position that is physically inside the player's own collision. Clearing depth
/// first means the weapon can never be eaten by geometry - which is the only way a first-person model behaves
/// predictably without doing real weapon collision, which this milestone deliberately has none of.</item>
/// <item>Projection. The viewmodel needs a much narrower field of view and a much smaller near plane than the
/// world. Sharing the world's projection would either distort the weapon or put it outside the depth range.</item>
/// </list>
/// </summary>
public sealed class FirstPersonWeaponRenderer : IDisposable
{
    /// <summary>Dark gunmetal: the launcher body, grip and rear housing.</summary>
    private static readonly Color BodyColor = new(58, 62, 70);

    /// <summary>Lighter metal: the barrel, muzzle and rails, so the silhouette separates from the body.</summary>
    private static readonly Color MetalColor = new(126, 132, 142);

    /// <summary>A single warm accent, on the sight and the side panels. Enough to break up the greys.</summary>
    private static readonly Color AccentColor = new(214, 122, 48);

    /// <summary>Near-black for the grip, so the shape reads as a held object rather than a floating block.</summary>
    private static readonly Color GripColor = new(30, 31, 36);

    private readonly WeaponViewModel _viewmodel;
    private readonly ViewModelTuning _tuning;
    private readonly BasicEffect _effect;

    // One mesh per material, each built once. The launcher's parts are separate meshes rather than one merged
    // mesh because they need different colours, and a per-part transform is cheaper than per-vertex colour
    // bookkeeping at draw time.
    private readonly GpuMesh _bodyBox;
    private readonly GpuMesh _barrelCylinder;
    private readonly GpuMesh _gripBox;
    private readonly GpuMesh _flashQuad;
    private bool _disposed;

    public FirstPersonWeaponRenderer(GraphicsDevice device, WeaponViewModel viewmodel, ViewModelTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(device);
        _viewmodel = viewmodel ?? throw new ArgumentNullException(nameof(viewmodel));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

        _bodyBox = GpuMesh.Create(device, new MeshBuilder().AddBox(Vector3.One, Color.White).Build());

        // Y-axis cylinder, one unit radius and two tall, so it can be scaled to any tube length.
        _barrelCylinder = GpuMesh.Create(device, new MeshBuilder().AddCylinder(1f, 2f, Color.White, segments: 12).Build());

        _gripBox = GpuMesh.Create(device, new MeshBuilder().AddBox(Vector3.One, Color.White).Build());

        _flashQuad = GpuMesh.Create(device, new MeshBuilder()
            .AddQuad(
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                Color.White)
            .Build());

        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = false,
            FogEnabled = false,
        };
    }

    /// <summary>
    /// Draws the weapon in view space.
    ///
    /// <paramref name="view"/> and <paramref name="projection"/> must be the viewmodel's own, not the world's - a
    /// narrower field of view and a centimetre-scale near plane. They are passed in rather than read from a camera
    /// so this class has no reference to one, and so nothing here can reach the player.
    /// </summary>
    /// <param name="viewModelWorld">Places the model: the camera basis plus the hold offset and recoil.</param>
    /// <param name="view">The viewmodel view matrix, from the camera's basis with no world translation.</param>
    /// <param name="projection">The viewmodel projection matrix.</param>
    public void Draw(Matrix viewModelWorld, Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            return;
        }

        GraphicsDevice device = _effect.GraphicsDevice;

        // Depth cleared by the caller before this pass; read-write here so the weapon's own parts occlude each
        // other correctly, which a depth-read state would not do.
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = World.FrontFaceState;
        device.BlendState = BlendState.AlphaBlend;

        _effect.View = view;
        _effect.Projection = projection;

        // The launcher, laid out along +Z (the direction the player is looking) with the origin at the grip. Sizes
        // are in metres and chosen to read at the configured viewmodel field of view from about half a metre away.
        DrawPart(_bodyBox, BodyColor, LaunchPlacement.RearHousing, new Vector3(0.062f, 0.058f, 0.15f), viewModelWorld);
        DrawPart(_bodyBox, BodyColor, LaunchPlacement.SideRail, new Vector3(0.016f, 0.018f, 0.19f), viewModelWorld);
        DrawPart(_bodyBox, AccentColor, LaunchPlacement.SightBlock, new Vector3(0.026f, 0.022f, 0.045f), viewModelWorld);

        // The grip hangs *below* and slightly behind the model origin, so that at the hold position the bottom of
        // the weapon sits near the bottom edge of the screen rather than cutting across the middle of it. A grip
        // centred on the origin put a bar of geometry straight through the player's view.
        DrawPart(_gripBox, GripColor, LaunchPlacement.Grip, new Vector3(0.042f, 0.085f, 0.05f), viewModelWorld);

        // The barrel is a cylinder rotated from its native +Y axis onto +Z. Its mesh is two units tall, so the
        // length in the size vector is half the length wanted.
        DrawBarrel(LaunchPlacement.BarrelCentre, new Vector3(0.036f, 0.10f, 0.036f), MetalColor, viewModelWorld);
        DrawBarrel(LaunchPlacement.MuzzleBrakeCentre, new Vector3(0.044f, 0.021f, 0.044f), BodyColor, viewModelWorld);

        DrawMuzzleFlash(viewModelWorld);
    }

    private void DrawPart(GpuMesh mesh, Color color, in Vector3 offset, in Vector3 size, Matrix viewModelWorld) =>
        DrawMesh(mesh, ModelMatrix(offset, size, Matrix.Identity, viewModelWorld), color, 1f);

    /// <summary>
    /// Draws a tube along the barrel. The mesh's axis is +Y, so it is rotated a quarter turn onto +Z and then
    /// scaled - scaling before rotating would squash the length along the wrong axis.
    /// </summary>
    private void DrawBarrel(in Vector3 offset, in Vector3 size, Color color, Matrix viewModelWorld) =>
        DrawMesh(_barrelCylinder, ModelMatrix(offset, size, RotateYToZ, viewModelWorld), color, 1f);

    /// <summary>
    /// The muzzle flash: two crossed camera-facing quads at the muzzle, scaled by how recently the shot was fired.
    /// Crossed rather than single so it is visible from any angle, and unlit so it is bright rather than shaded.
    /// </summary>
    private void DrawMuzzleFlash(Matrix viewModelWorld)
    {
        if (!_viewmodel.IsFlashing)
        {
            return;
        }

        float strength = _viewmodel.MuzzleFlashStrength;
        float size = _tuning.MuzzleFlashSize * (0.45f + (strength * 0.55f));

        Matrix placement = ModelMatrix(LaunchPlacement.MuzzleTip, new Vector3(size, size, size), Matrix.Identity, Matrix.Identity);

        DrawMesh(_flashQuad, placement * viewModelWorld, new Color(255, 236, 180), strength);
        DrawMesh(_flashQuad, placement * viewModelWorld * RotateYToZ, new Color(255, 190, 96), strength * 0.85f);
    }

    /// <summary>
    /// The model matrix for one part.
    ///
    /// Order matters and is deliberate: the part's own scale and offset are applied <i>first</i>, in model space,
    /// and the recoil and rest tilt <i>after</i>, in view space. That way a part's offset is measured in metres
    /// regardless of how big the part is, and the whole weapon pivots about the eye as one piece rather than each
    /// part sliding independently - which is what a recoil should look like.
    /// </summary>
    private Matrix ModelMatrix(in Vector3 offset, in Vector3 size, in Matrix extraRotation, in Matrix viewModelWorld)
    {
        Matrix recoil = Matrix.CreateFromYawPitchRoll(
            _viewmodel.RecoilRotation.Y,
            _viewmodel.RecoilRotation.X,
            _viewmodel.RecoilRotation.Z);

        Matrix rest = Matrix.CreateFromYawPitchRoll(
            _tuning.RestRotation.Y,
            _tuning.RestRotation.X,
            _tuning.RestRotation.Z);

        // The viewmodel world matrix already carries the hold position and the recoil *translation*, so it is
        // composed in last. Rebuilding it here instead would have doubled the offset and put the launcher well
        // outside the near plane.
        return Matrix.CreateScale(size)
            * extraRotation
            * Matrix.CreateTranslation(offset)
            * Matrix.CreateTranslation(_viewmodel.RecoilOffset)
            * recoil
            * rest
            * viewModelWorld;
    }

    private void DrawMesh(GpuMesh mesh, Matrix world, Color color, float alpha = 1f)
    {
        _effect.World = world;
        _effect.DiffuseColor = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        _effect.Alpha = Math.Clamp(alpha, 0f, 1f);

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }

        mesh.Draw(_effect.GraphicsDevice);
    }

    /// <summary>A quarter turn taking the cylinder mesh's native +Y axis onto +Z, where the barrel points.</summary>
    private static Matrix RotateYToZ => Matrix.CreateRotationX(-MathHelper.PiOver2);

    /// <summary>
    /// Part offsets in metres, relative to the grip, along the barrel. Laid out so the silhouette reads as a
    /// shoulder-fired launcher: a fat tube, a boxy rear housing, and a grip hanging below.
    /// </summary>
    private static class LaunchPlacement
    {
        public static Vector3 RearHousing => new(0f, 0.012f, -0.02f);

        public static Vector3 SideRail => new(0.058f, 0.03f, 0.02f);

        public static Vector3 SightBlock => new(0f, 0.062f, 0.06f);

        public static Vector3 Grip => new(-0.004f, -0.058f, -0.055f);

        public static Vector3 BarrelCentre => new(0f, 0.015f, 0.20f);

        public static Vector3 MuzzleBrakeCentre => new(0f, 0.015f, 0.305f);

        public static Vector3 MuzzleTip => new(0f, 0.015f, 0.34f);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bodyBox.Dispose();
        _barrelCylinder.Dispose();
        _gripBox.Dispose();
        _flashQuad.Dispose();
        _effect.Dispose();
    }
}
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Character;
using MovementShooter.Graphics;
using MovementShooter.Map;
using MovementShooter.Weapons;

namespace MovementShooter.UI;

/// <summary>
/// Draws the first-person viewmodel: the canonical launcher plus a pair of arms holding it.
///
/// <para>
/// Both meshes are the same assets the third-person character uses. The launcher is literally
/// <c>KINETIC_RocketLauncher</c> - the identical <see cref="WeaponAsset"/> instance the character's
/// <see cref="WeaponAttachment"/> draws - so there is one weapon design in the project, shown from
/// two viewpoints. This renderer used to build a launcher out of boxes and cylinders instead,
/// which meant the player fired one weapon while looking at another.
/// </para>
///
/// <para>
/// Drawn in a <b>separate pass from the world</b>, after the depth buffer has been cleared. Two reasons,
/// both load-bearing:
/// </para>
/// <list type="number">
/// <item>Depth. With the world's depth still in place, standing against a wall would clip the weapon into
/// it, because the model is held at a position that is physically inside the player's own collision.
/// Clearing depth first means the weapon can never be eaten by geometry - which is the only way a
/// first-person model behaves predictably without giving it real collision, which this milestone
/// deliberately has none of.</item>
/// <item>Projection. The viewmodel needs a much narrower field of view and a much smaller near plane
/// than the world. Sharing the world's projection would either distort the weapon or put it outside the
/// depth range.</item>
/// </list>
/// </summary>
public sealed class FirstPersonWeaponRenderer : IDisposable
{
    private readonly WeaponViewModel _viewmodel;
    private readonly ViewModelTuning _tuning;
    private readonly WeaponRenderer _launcher;
    private readonly WeaponRenderer _arms;
    private readonly BasicEffect _effect;
    private readonly GpuMesh _flashQuad;
    private readonly Vector3 _muzzle;
    private bool _disposed;

    /// <param name="launcher">The canonical launcher, shared with the third-person character.</param>
    /// <param name="arms">First-person arms, authored in the launcher's space so one transform places both.</param>
    public FirstPersonWeaponRenderer(
        GraphicsDevice device,
        WeaponViewModel viewmodel,
        ViewModelTuning tuning,
        WeaponAsset launcher,
        WeaponAsset arms)
    {
        ArgumentNullException.ThrowIfNull(device);
        _viewmodel = viewmodel ?? throw new ArgumentNullException(nameof(viewmodel));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(arms);

        if (!launcher.HasAuthoredMuzzle)
        {
            throw new ArgumentException(
                "The launcher has no authored muzzle, so the flash would have nowhere correct to appear.",
                nameof(launcher));
        }

        // Both are drawn through the same renderer the character uses, so the first-person
        // weapon is lit by the same material and the same vertex colours as the third-person
        // one. Anything else and the same asset reads as two different objects.
        _launcher = new WeaponRenderer(device, launcher);
        _arms = new WeaponRenderer(device, arms);

        // The flash is not part of either asset: it is an effect with a lifetime, and baking it
        // into the weapon would make it permanent geometry.
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

        _muzzle = launcher.Muzzle;
    }

    /// <summary>The canonical launcher's asset, for diagnostics and checks.</summary>
    public WeaponAsset Launcher => _launcher.Asset;

    /// <summary>The first-person arms' asset, for diagnostics and checks.</summary>
    public WeaponAsset Arms { get; }

    /// <summary>
    /// The muzzle, in weapon space, taken from the canonical launcher's authored value.
    ///
    /// Exposed so the flash lands at the end of the barrel and so a check can confirm the
    /// first-person weapon reports the same muzzle the third-person one does. It is
    /// presentation: gameplay spawns rockets at the character's <c>MuzzlePoint</c> socket, which is
    /// the only muzzle that is a real physical fact.
    /// </summary>
    public Vector3 Muzzle => _muzzle;

    /// <summary>Where the muzzle ends up this frame, in view space.</summary>
    public Vector3 MuzzleInViewSpace(Matrix viewModelWorld) => Vector3.Transform(_muzzle, viewModelWorld);

    /// <summary>
    /// Draws the viewmodel in view space.
    ///
    /// <paramref name="viewModelWorld"/> places the whole assembly. <paramref name="view"/> and
    /// <paramref name="projection"/> must be the viewmodel's own, not the world's. They are passed
    /// in rather than read from a camera so this class has no reference to one, and so nothing here
    /// can reach the player.
    /// </summary>
    public void Draw(Matrix viewModelWorld, Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            return;
        }

        // The launcher first, then the arms over it: the arms wrap the grip and the fore-grip, so
        // they are the nearer surface on the parts they hold.
        _launcher.Draw(viewModelWorld, view, projection);
        _arms.Draw(viewModelWorld, view, projection);

        DrawMuzzleFlash(viewModelWorld, view, projection);
    }

    /// <summary>
    /// The muzzle flash: two crossed quads at the authored muzzle, scaled by how recently the shot
    /// was fired. Crossed so it is visible from any angle, and unlit so it is bright rather than
    /// shaded.
    /// </summary>
    private void DrawMuzzleFlash(Matrix viewModelWorld, Matrix view, Matrix projection)
    {
        if (!_viewmodel.IsFlashing)
        {
            return;
        }

        float strength = _viewmodel.MuzzleFlashStrength;
        float size = _tuning.MuzzleFlashSize * (0.45f + (strength * 0.55f));

        GraphicsDevice device = _effect.GraphicsDevice;
        device.BlendState = BlendState.AlphaBlend;

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.CreateTranslation(_muzzle)
            * Matrix.CreateScale(size)
            * viewModelWorld;

        DrawFlash(device, new Color(255, 236, 180), strength);
        DrawFlash(device, new Color(255, 190, 96), strength * 0.85f, RotateYToZ);
    }

    private void DrawFlash(GraphicsDevice device, Color color, float alpha, Matrix? extra = null)
    {
        Matrix world = _effect.World;
        if (extra.HasValue)
        {
            _effect.World = extra.Value * world;
        }

        _effect.DiffuseColor = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        _effect.Alpha = Math.Clamp(alpha, 0f, 1f);

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }

        _flashQuad.Draw(device);
        _effect.World = world;
    }

    /// <summary>A quarter turn, so the second flash quad is edge-on to the first.</summary>
    private static Matrix RotateYToZ => Matrix.CreateRotationX(-MathHelper.PiOver2);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _launcher.Dispose();
        _arms.Dispose();
        _flashQuad.Dispose();
        _effect.Dispose();
    }
}
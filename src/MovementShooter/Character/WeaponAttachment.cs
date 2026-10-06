using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Character;

/// <summary>
/// A weapon carried on a character, attached through that character's socket system.
///
/// The attachment is deliberately indirect: this never touches a bone and never holds a
/// world transform of its own. Each frame it asks the character where its sockets are,
/// via <see cref="CharacterBody.TryGetSocketWorld"/>, and places the weapon there. Two
/// consequences fall out of that, and both are the point:
///
/// <list type="bullet">
/// <item>The weapon follows the animation for free, because the sockets already do. Run,
/// Slide, Dash, Jump and Fire all move the arm, so the launcher moves with them without
/// a single per-clip weapon pose.</item>
/// <item>A future second character - another player in multiplayer - attaches its own
/// launcher by handing this a different <see cref="CharacterBody"/>. Nothing here is
/// specific to the player.</item>
/// </list>
///
/// Presentation only. This reads the character and draws; it cannot influence movement,
/// collision or aim.
/// </summary>
public sealed class WeaponAttachment : IDisposable
{
    /// <summary>Socket the launcher hangs from. Right hand on this rig.</summary>
    public const string WeaponSocketName = "WeaponSocket";

    /// <summary>Socket the support hand meets. Left hand on this rig.</summary>
    public const string SupportSocketName = "WeaponSocketSupport";

    /// <summary>Socket a projectile leaves from. Nested under the weapon socket.</summary>
    public const string MuzzleSocketName = "MuzzlePoint";

    private readonly WeaponRenderer _renderer;
    private readonly WeaponAsset _asset;
    private Matrix _weaponWorld = Matrix.Identity;
    private bool _attached;
    private bool _disposed;

    public WeaponAttachment(GraphicsDevice device, WeaponAsset asset)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(asset);

        _asset = asset;
        _renderer = new WeaponRenderer(device, asset);
    }

    public WeaponAsset Asset => _asset;

    /// <summary>Draw calls per frame, one per material.</summary>
    public int SubmeshCount => _renderer.SubmeshCount;

    /// <summary>
    /// World transform of the launcher from the last <see cref="Update"/>, or identity
    /// before the first.
    /// </summary>
    public Matrix WeaponWorld => _weaponWorld;

    /// <summary>True once the character has been found to actually carry the sockets.</summary>
    public bool IsAttached => _attached;

    /// <summary>Where a projectile should appear, from the character's MuzzlePoint socket.</summary>
    public bool TryGetMuzzle(out Vector3 world)
    {
        if (_body is not null && _body.TryGetSocketWorld(MuzzleSocketName, out Matrix muzzle))
        {
            world = Vector3.Transform(Vector3.Zero, muzzle);
            return true;
        }

        world = Vector3.Zero;
        return false;
    }

    /// <summary>Where the support hand meets the weapon, from WeaponSocketSupport.</summary>
    public bool TryGetSupportPoint(out Vector3 world)
    {
        if (_body is not null && _body.TryGetSocketWorld(SupportSocketName, out Matrix support))
        {
            world = Vector3.Transform(Vector3.Zero, support);
            return true;
        }

        world = Vector3.Zero;
        return false;
    }

    private CharacterBody? _body;

    /// <summary>
    /// Binds this weapon to a character. Call once; the attachment then tracks that
    /// character every frame.
    /// </summary>
    public void Attach(CharacterBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
    }

    /// <summary>
    /// Resolves the weapon's world transform from the character's sockets. Call after the
    /// character has been updated, so the pose this reads is the one being drawn.
    /// </summary>
    public void Update()
    {
        ThrowIfDisposed();

        if (_body is null)
        {
            return;
        }

        // The weapon's origin is its grip and it points along its own +Z, matching the
        // character's convention, so the socket's world matrix is used verbatim. The
        // authored rotation on the socket is what orients it.
        _attached = _body.TryGetSocketWorld(WeaponSocketName, out _weaponWorld);
    }

    /// <summary>
    /// Draws the weapon. Call inside the opaque world pass, with the character.
    ///
    /// Skipped when the character has no weapon socket, rather than drawn at the origin -
    /// a character without sockets is a valid state and a launcher at the world origin
    /// would be far worse than no launcher.
    /// </summary>
    public void Draw(Matrix view, Matrix projection)
    {
        ThrowIfDisposed();

        if (_attached)
        {
            _renderer.Draw(_weaponWorld, view, projection);
        }
    }

    /// <summary>
    /// Draws the launcher at an explicitly supplied world matrix, bypassing attachment.
    ///
    /// Exists for the offscreen preview, which poses a skeleton directly rather than
    /// through a <see cref="CharacterBody"/> and therefore has no sockets to resolve. The
    /// preview composes the socket world matrix from the posed skeleton itself, which is
    /// the same value <see cref="Update"/> produces in game.
    /// </summary>
    public void DrawAt(Matrix world, Matrix view, Matrix projection)
    {
        ThrowIfDisposed();
        _renderer.Draw(world, view, projection);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WeaponAttachment));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _renderer.Dispose();
    }
}
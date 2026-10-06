using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Character;

/// <summary>
/// One full-body character in the world: asset, skeleton, skin, animator and GPU buffers.
///
/// This is the whole character-facing surface. It is told where the owner is and what it is
/// doing, and it draws itself - it never reads physics, never writes movement, and holds no
/// reference to the player. That is what lets the same type serve the local player now and
/// remote players later: the only difference between them is who supplies the transform.
///
/// Translation is entirely the caller's. Clips are in place, so this composes
/// scale, then translation, then yaw - in row-vector order that is
/// <c>Scale * Translate * RotateY</c>. It derives nothing from velocity and applies no root
/// motion, because movement is authoritative in <c>PlayerMovement</c> and a second source of
/// truth for position is how a character ends up fighting its own collider.
/// </summary>
public sealed class CharacterBody : IDisposable
{
    private readonly CharacterTuning _tuning;
    private readonly CharacterAsset _asset;
    private readonly CharacterSkeleton _skeleton;
    private readonly CharacterSkeleton _blendScratch;
    private readonly CharacterSkin _skin;
    private readonly CharacterAnimator _animator;
    private readonly CharacterRenderer _renderer;
    private bool _disposed;

    public CharacterBody(GraphicsDevice device, CharacterAsset asset, CharacterTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(tuning);

        tuning.Validate();

        _tuning = tuning;
        _asset = asset;
        _skeleton = new CharacterSkeleton(asset);
        _blendScratch = new CharacterSkeleton(asset);
        _skin = new CharacterSkin(asset);
        _animator = new CharacterAnimator(asset, _skeleton, _blendScratch);
        _renderer = new CharacterRenderer(device, asset, _skin);

        // Pose once so the first draw has real data rather than the seed buffer.
        _animator.Update(0f, new CharacterAnimationInput(
            IsGrounded: true, VerticalSpeed: 0f, HorizontalSpeed: 0f,
            IsSliding: false, IsDashing: false, FireTriggered: false),
            tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        _skin.Skin(_skeleton);
        _renderer.Update(_skin);
    }

    public CharacterAnimator Animator => _animator;

    public CharacterAsset Asset => _asset;

    /// <summary>Authored height in metres, before <see cref="CharacterTuning.Scale"/>.</summary>
    public float AuthoredHeight => _asset.Height;

    /// <summary>Height in metres as drawn.</summary>
    public float ScaledHeight => _asset.Height * _tuning.Scale;

    /// <summary>Clip currently playing, for diagnostics and the debug overlay.</summary>
    public CharacterAnim CurrentAnimation => _animator.Current;

    /// <summary>
    /// Advances animation and re-uploads the skinned mesh.
    ///
    /// <paramref name="stanceHeight"/> is the owner's live collider height. The character's
    /// origin is at its feet while the collider's is at its centre, so the feet are placed by
    /// dropping half the stance height - using the live value rather than the standing one
    /// keeps the soles planted while the capsule shrinks for a crouch or a slide.
    /// </summary>
    public void Update(float deltaSeconds, in CharacterAnimationInput input, Vector3 bodyPosition,
        float stanceHeight, float yawRadians)
    {
        ThrowIfDisposed();

        _animator.Update(deltaSeconds, input, _tuning.RunSpeedForFullRate, _tuning.BlendSeconds);
        _skin.Skin(_skeleton);
        _renderer.Update(_skin);
        _lastWorld = ComposeWorld(bodyPosition, stanceHeight, yawRadians);
    }

    private Matrix _lastWorld;

    /// <summary>World matrix from the most recent <see cref="Update"/>, or identity before the first.</summary>
    public Matrix WorldMatrix => _lastWorld;

    /// <summary>
    /// World transform of an attachment point, in the same space as <see cref="WorldMatrix"/>.
    ///
    /// This is what a weapon or a muzzle flash attaches to. Sockets are Empties parented to
    /// bones rather than bones themselves, so this resolves through the posed skeleton and
    /// applies the character's world placement on top - a socket returned in model space
    /// would ignore where the character is standing and facing.
    ///
    /// Returns false when the character has no such socket, so callers can fall back
    /// rather than silently drawing at the origin.
    /// </summary>
    public bool TryGetSocketWorld(string name, out Matrix world)
    {
        ThrowIfDisposed();

        int socket = _asset.IndexOfSocket(name);
        if (socket < 0)
        {
            world = Matrix.Identity;
            return false;
        }

        world = _skeleton.SocketWorld(socket) * _lastWorld;
        return true;
    }

    /// <summary>Draws the character. Call inside the opaque world pass.</summary>
    public void Draw(Matrix view, Matrix projection)
    {
        ThrowIfDisposed();
        _renderer.Draw(_lastWorld, view, projection);
    }

    /// <summary>
    /// Places the character: scale, then translate to the owner's feet, then yaw.
    ///
    /// The character rig was authored facing +Z, and <c>FlatForward</c> at yaw 0 is also +Z,
    /// so the rotation needs no extra correction - a 180-degree error here is the single
    /// easiest mistake to make and shows up as a character running backwards.
    /// </summary>
    public Matrix ComposeWorld(Vector3 bodyPosition, float stanceHeight, float yawRadians)
    {
        // The collider's origin is its centre; the character's is its feet. Dropping half
        // the live stance height puts the soles on the floor, and using the live value
        // rather than the standing one keeps them there through a crouch or a slide.
        Vector3 feet = bodyPosition;
        feet.Y -= stanceHeight * 0.5f;

        return Matrix.CreateScale(_tuning.Scale)
            * Matrix.CreateTranslation(feet)
            * Matrix.CreateRotationY(yawRadians);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CharacterBody));
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

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Combat;
using MovementShooter.Graphics;
using MovementShooter.Map;
using MovementShooter.Weapons;

namespace MovementShooter.UI;

/// <summary>
/// Draws rockets in flight, their trails and flames, and explosions. Procedural geometry only: no texture, no
/// content pipeline, no particle framework.
/// </summary>
/// <remarks>
/// Two shared meshes - a low-segment cylinder for rocket bodies and a unit quad for every billboard - and one
/// unlit effect whose colour is set per draw. That keeps a rocket to two draw calls and a blast to three, which is
/// far cheaper than a mesh per object and cheap enough to leave switched on permanently.
/// <para>
/// Reads only from <see cref="ProjectileSystem"/> and <see cref="ExplosionSystem"/>. Like the movement effects it
/// holds no reference to the physics world and cannot affect the simulation.
/// </para>
/// </remarks>
public sealed class CombatEffectRenderer : IDisposable
{
    private static readonly Color BodyColor = new(214, 216, 224);
    private static readonly Color CoreColor = new(255, 214, 120);
    private static readonly Color FlameColor = new(255, 158, 62);
    private static readonly Color TrailColor = new(255, 196, 130);
    private static readonly Color ShellColor = new(255, 132, 48);
    private static readonly Color FlashColor = new(255, 246, 214);
    private static readonly Color RingColor = new(255, 196, 120);

    /// <summary>Seconds an explosion's visuals last.</summary>
    private const float BlastSeconds = 0.42f;

    /// <summary>Trail length as a multiple of the drawn rocket radius.</summary>
    private const float TrailLengthScale = 14f;

    /// <summary>
    /// How much bigger than its collision radius a rocket is drawn. The 12 cm collision radius is right for
    /// physics and hopeless for seeing: from across the arena the player has to be able to follow the rocket from
    /// the launcher to the impact. The visual is deliberately larger than the collider, and this is the single
    /// place that discrepancy lives.
    /// </summary>
    private const float VisualScale = 2.4f;

    /// <summary>The camera's world position this frame, used to orient every billboard.</summary>
    private Vector3 _eye;

    private readonly ProjectileSystem _projectiles;
    private readonly ExplosionSystem _explosions;

    private readonly GpuMesh _shape;
    private readonly GpuMesh _quad;
    private readonly BasicEffect _effect;
    private bool _disposed;

    public CombatEffectRenderer(GraphicsDevice device, ProjectileSystem projectiles, ExplosionSystem explosions)
    {
        ArgumentNullException.ThrowIfNull(device);
        _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        _explosions = explosions ?? throw new ArgumentNullException(nameof(explosions));

        // Few segments: a rocket is small at any distance the player sees it from, and its silhouette matters far
        // more than whether it is round.
        _shape = GpuMesh.Create(device, new MeshBuilder().AddCylinder(1f, 2f, Color.White, segments: 8).Build());

        _quad = GpuMesh.Create(device, new MeshBuilder()
            .AddQuad(
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                Color.White)
            .Build());

        // Unlit on purpose: these are flames, flashes and streaks. Lighting them would only dim them in shadow, and
        // the point is that a rocket and its impact are visible the instant they happen.
        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = false,
            FogEnabled = false,
        };
    }

    /// <summary>Draws every live rocket and the most recent blast.</summary>
    public void Draw(Matrix view, Matrix projection)
    {
        if (_disposed)
        {
            return;
        }

        GraphicsDevice device = _effect.GraphicsDevice;

        // Depth tested but not written: these are transparent, and they must still be hidden by geometry in
        // front of them.
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.RasterizerState = World.FrontFaceState;
        device.BlendState = BlendState.AlphaBlend;

_effect.View = view;
        _effect.Projection = projection;

        // The camera's position this frame, recovered from the view matrix, so every billboard knows which way it
        // has to face. Recorded once here rather than passed through every call.
        _eye = new Vector3(view.M41, view.M42, view.M43);

        foreach (Projectile projectile in _projectiles.Live)
        {
            DrawRocket(projectile, view);
        }

        DrawExplosion(view);
    }

    /// <summary>
    /// A rocket: a small cylinder along its direction of travel, a bright core at the nose, and a flame plus trail
    /// behind it. The body is oriented along travel so it reads as going somewhere, not merely existing.
    /// </summary>
    private void DrawRocket(in Projectile projectile, Matrix view)
    {
        Vector3 direction = projectile.Velocity.LengthSquared() > 1e-6f
            ? Vector3.Normalize(projectile.Velocity)
            : Vector3.Forward;

        float radius = MathF.Max(projectile.Radius * VisualScale, 0.09f);

        // The cylinder mesh is one unit radius and two units tall, centred on the origin, axis along +Y. Scaling it
        // uniformly and rotating +Y onto the direction of travel puts its nose ahead of the swept point, which is
        // where the leading edge of a rocket actually is.
        Matrix world = Matrix.CreateScale(radius, radius, radius)
            * OrientYAxis(direction)
            * Matrix.CreateTranslation(projectile.Position);

        DrawMesh(_shape, world, BodyColor, 1f);

        // Bright core at the nose: what the player actually tracks across the arena.
        DrawBillboard(view, projectile.Position + (direction * (radius * 1.1f)), radius * 2.6f, radius * 2.6f, CoreColor, 0.95f);

        // Flame, then a longer dimmer trail. The difference between the two is what makes the rocket read as
        // moving rather than gliding.
        float flame = radius * 5f;
        DrawBillboard(view, projectile.Position - (direction * (flame * 0.5f)), radius * 2.2f, flame, FlameColor, 0.8f, direction);

        float halfTrail = radius * TrailLengthScale;
        DrawBillboard(
            view,
            projectile.Position - (direction * (halfTrail + (radius * 4f))),
            radius * 1.4f,
            halfTrail * 2f,
            TrailColor,
            0.3f,
            direction);
    }

    /// <summary>
    /// An explosion: a hot core, an expanding shell, and a ground ring that keeps expanding after the core has
    /// gone.
    ///
    /// Three quads. The ring is what makes a blast read as an explosion rather than a bright dot - a flat ring
    /// spreading outwards is the cheapest cue a player reliably reads as a shockwave, and it is one more quad.
    /// </summary>
    private void DrawExplosion(Matrix view)
    {
        Explosion? last = _explosions.LastExplosion;
        if (last is null)
        {
            return;
        }

        float age = _explosions.ElapsedSeconds - _explosions.LastExplosionSeconds;
        if (age < 0f || age > BlastSeconds)
        {
            return;
        }

        float t = age / BlastSeconds;

        // Ease-out expansion and a squared fade: reaches full size early and then dissolves, which is what makes
        // a blast read as a shockwave passing through rather than a ball slowly inflating.
        float expand = 1f - ((1f - t) * (1f - t));
        float fade = (1f - t) * (1f - t);

        Vector3 centre = _explosions.LastExplosionCentre;
        float baseRadius = last.Value.Radius * 0.4f;

        DrawBillboard(view, centre, baseRadius * expand * 2f, baseRadius * expand * 2f, ShellColor, fade * 0.5f);

        float core = baseRadius * expand * 1.3f;
        DrawBillboard(view, centre, core * 2f, core * 2f, FlashColor, fade * fade);

        // The ring sits flat on the ground so it stays readable from the player's eye height, and it outruns the
        // flash: by the time the core has gone the ring is still spreading.
        float ringRadius = baseRadius * (0.6f + (expand * 2.4f));
        DrawBillboard(view, centre + new Vector3(0f, 0.15f, 0f), ringRadius * 2f, ringRadius * 2f, RingColor, fade * 0.45f, Vector3.Up);
    }

    /// <summary>
    /// Draws a camera-facing quad. Orientation comes from <see cref="Billboards"/>, which builds a full orthonormal
    /// basis - a singular one shades to black, which is how the health bars went wrong.
    /// </summary>
    private void DrawBillboard(
        Matrix view,
        Vector3 centre,
        float width,
        float height,
        Color color,
        float alphaScale,
        Vector3? upOverride = null)
    {
        float alpha = color.A / 255f * alphaScale;
        if (alpha <= 0.001f)
        {
            return;
        }

        DrawMesh(_quad, Billboards.FaceCamera(view, _eye, centre, width, height, upOverride), color, alpha);
    }

    /// <summary>
    /// A rotation taking local +Y onto <paramref name="direction"/>, built as an orthonormal basis rather than
    /// via a quaternion: MonoGame's <c>Quaternion</c> has no "from one unit vector to another" helper, and the
    /// basis is both shorter and predictable. One degenerate case needs handling - a direction parallel to +X
    /// leaves no perpendicular - so it falls back to +Z.
    /// </summary>
    private static Matrix OrientYAxis(Vector3 direction)
    {
        Vector3 up = Vector3.Normalize(direction);

        Vector3 reference = MathF.Abs(up.X) < 0.9f ? Vector3.Right : Vector3.Forward;
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, reference));
        Vector3 forward = Vector3.Cross(right, up);

        // Rows are the basis vectors, in MonoGame's row-vector convention.
        return new Matrix(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f);
    }

    private void DrawMesh(GpuMesh mesh, Matrix world, Color color, float alpha)
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shape.Dispose();
        _quad.Dispose();
        _effect.Dispose();
    }
}

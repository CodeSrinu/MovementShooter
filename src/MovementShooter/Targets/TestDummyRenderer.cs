using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Graphics;
using MovementShooter.Map;

namespace MovementShooter.Targets;

/// <summary>
/// Draws the test dummies: a body, plus a health bar that drains as it takes damage - enough to read at a
/// glance whether an explosion reached a target and how hard.
/// </summary>
/// <remarks>
/// Entirely procedural and untextured. The bar is a unit quad scaled along one axis, so a single mesh serves
/// every dummy at any health, and the billboard basis comes straight out of the view matrix rather than being
/// rebuilt per frame.
/// <para>
/// Per-object colour goes through a small cached material per colour rather than a vertex-colour buffer. The
/// meshes are uploaded once and shared, and the palette is a fixed handful of values, so this stays a lookup
/// rather than anything per-frame.
/// </para>
/// </remarks>
public sealed class TestDummyRenderer : IDisposable
{
    private const float BarWidth = 1.6f;
    private const float BarHeight = 0.18f;
    private const float BarClearance = 0.4f;

    private static readonly Color AliveColor = new(212, 98, 74);
    private static readonly Color DeadColor = new(80, 80, 90);
    private static readonly Color BarBackground = new(18, 18, 22);
    private static readonly Color BarHealthy = new(96, 200, 120);
    private static readonly Color BarWounded = new(230, 190, 80);
    private static readonly Color BarCritical = new(220, 80, 70);

    private readonly GraphicsDevice _device;
    private readonly GpuMesh _unitQuad;
    private readonly GpuMesh _body;
    private readonly Dictionary<Color, SurfaceMaterial> _materials = new();
    private bool _disposed;

    public TestDummyRenderer(GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        _unitQuad = GpuMesh.Create(device, new MeshBuilder()
            .AddQuad(
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                Color.White)
            .Build());

        _body = GpuMesh.Create(device, new MeshBuilder().AddBox(Vector3.One, Color.White).Build());
    }

    public void Draw(IReadOnlyList<TestDummy> dummies, Matrix view, Matrix projection)
    {
        if (_disposed || dummies.Count == 0)
        {
            return;
        }

        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = World.FrontFaceState;
        _device.BlendState = BlendState.AlphaBlend;

        Vector3 eye = CameraPosition(view);
        Vector3 right = CameraRight(view);

        foreach (TestDummy dummy in dummies)
        {
            DrawBody(dummy, view, projection);
            DrawHealthBar(dummy, right, eye, view, projection);
        }
    }

    private void DrawBody(TestDummy dummy, Matrix view, Matrix projection)
    {
        Color color = dummy.Health.IsDead ? DeadColor : AliveColor;

        // A box rather than another character capsule: the thing being aimed at should read as an object, not
        // as a second player.
        Matrix world = Matrix.CreateScale(dummy.Size) * Matrix.CreateTranslation(dummy.Position);
        Draw(world, color, _body, view, projection);
    }

    private void DrawHealthBar(TestDummy dummy, Vector3 right, Vector3 eye, Matrix view, Matrix projection)
    {
        Vector3 centre = dummy.Position + new Vector3(0f, (dummy.Size * 0.5f) + BarClearance, 0f);

        // Background first, then the fill on top, both in the same plane.
        DrawBillboard(centre, BarWidth, BarHeight, BarBackground, eye, view, projection);

        float fraction = MathClampHealth(dummy);
        float fillWidth = BarWidth * fraction;

        if (fillWidth <= 0.001f)
        {
            return;
        }

        // Anchored left, so the bar drains from the right - the convention players already expect.
        Vector3 fillCentre = centre - (right * ((BarWidth - fillWidth) * 0.5f));

        Color fill = fraction > 0.5f ? BarHealthy : fraction > 0.25f ? BarWounded : BarCritical;
        DrawBillboard(fillCentre, fillWidth, BarHeight * 0.7f, fill, eye, view, projection);
    }

    private static float MathClampHealth(TestDummy dummy) =>
        Math.Clamp(dummy.Health.CurrentHealth / dummy.Health.MaxHealth, 0f, 1f);

    /// <summary>
    /// A camera-facing quad. The orientation comes from <see cref="Billboards"/>, which builds a full orthonormal
    /// basis; the previous hand-rolled basis here left the third row zero, making the matrix singular and shading
    /// the health bars to black.
    /// </summary>
private void DrawBillboard(Vector3 centre, float width, float height, Color color, Vector3 eye, Matrix view, Matrix projection) =>
        Draw(Billboards.FaceCamera(view, eye, centre, width, height), color, _unitQuad, view, projection);

    private static Vector3 CameraRight(Matrix view) =>
        Vector3.Normalize(Vector3.TransformNormal(Vector3.Right, view));

    /// <summary>
    /// The camera's world position, recovered from the view matrix. Every view matrix carries the negated eye in
    /// its translation row, so this is exact rather than an approximation.
    /// </summary>
    private static Vector3 CameraPosition(Matrix view) => new(view.M41, view.M42, view.M43);

    private void Draw(Matrix world, Color color, GpuMesh mesh, Matrix view, Matrix projection)
    {
        MaterialFor(color).Apply(world, view, projection);
        MaterialFor(color).Draw(mesh);
    }

    private SurfaceMaterial MaterialFor(Color color)
    {
        if (!_materials.TryGetValue(color, out SurfaceMaterial? material))
        {
            material = SurfaceMaterial.CreateLit(_device, color);
            _materials.Add(color, material);
        }

        return material;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (SurfaceMaterial material in _materials.Values)
        {
            material.Dispose();
        }

        _materials.Clear();
        _unitQuad.Dispose();
        _body.Dispose();
    }
}

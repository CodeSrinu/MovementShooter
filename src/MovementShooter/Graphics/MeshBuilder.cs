using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Graphics;

/// <summary>
/// Procedural geometry factory. Every shape in the game (and later every map block) is generated in
/// code, which keeps the repository free of binary assets and makes geometry easy for both humans and
/// AI to reason about.
///
/// Winding convention: every polygon is counter-clockwise when viewed from outside the shape (the
/// right-handed convention, so normals can be checked by hand with a cross product). The renderer pairs
/// this with <see cref="Map.World.FrontFaceState"/> - see the note there.
/// </summary>
public sealed class MeshBuilder
{
    private readonly List<VertexPositionColorNormal> _vertices = new();
    private readonly List<int> _indices = new();

    public MeshData Build() => new(_vertices.ToArray(), _indices.ToArray());

    /// <summary>Appends a counter-clockwise quad (a, b, c, d) with a flat normal.</summary>
    public MeshBuilder AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) =>
        AddPolygon(FaceNormal(a, b, c), color, a, b, c, d);

    /// <summary>Appends a counter-clockwise triangle with a flat normal.</summary>
    public MeshBuilder AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color) =>
        AddPolygon(FaceNormal(a, b, c), color, a, b, c);

    private static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c) =>
        Vector3.Normalize(Vector3.Cross(b - a, c - a));

    private MeshBuilder AddPolygon(Vector3 normal, Color color, params Vector3[] points)
    {
        int start = _vertices.Count;
        foreach (Vector3 point in points)
        {
            _vertices.Add(new VertexPositionColorNormal(point, color, normal));
        }

        for (int i = 2; i < points.Length; i++)
        {
            _indices.Add(start);
            _indices.Add(start + i - 1);
            _indices.Add(start + i);
        }

        return this;
    }

    /// <summary>Axis-aligned box centred on the origin, built from six flat-shaded quads.</summary>
    public MeshBuilder AddBox(Vector3 size, Color color)
    {
        if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Box dimensions must be positive.");
        }

        Vector3 h = size * 0.5f;
        Vector3 p000 = new(-h.X, -h.Y, -h.Z);
        Vector3 p100 = new(h.X, -h.Y, -h.Z);
        Vector3 p110 = new(h.X, h.Y, -h.Z);
        Vector3 p010 = new(-h.X, h.Y, -h.Z);
        Vector3 p001 = new(-h.X, -h.Y, h.Z);
        Vector3 p101 = new(h.X, -h.Y, h.Z);
        Vector3 p111 = new(h.X, h.Y, h.Z);
        Vector3 p011 = new(-h.X, h.Y, h.Z);

        AddQuad(p001, p101, p111, p011, color); // +Z
        AddQuad(p100, p000, p010, p110, color); // -Z
        AddQuad(p101, p100, p110, p111, color); // +X
        AddQuad(p000, p001, p011, p010, color); // -X
        AddQuad(p011, p111, p110, p010, color); // +Y (top)
        AddQuad(p000, p100, p101, p001, color); // -Y (bottom)
        return this;
    }

    /// <summary>
    /// Wedge used for jump ramps. Cross-section in the ZY plane is a right triangle rising from
    /// y=0 at -Z to y=height at +Z, extruded along X and centred on the origin.
    /// </summary>
    public MeshBuilder AddRamp(Vector2 footprint, float height, Color color)
    {
        float x = footprint.X * 0.5f;
        float z = footprint.Y * 0.5f;

        Vector3 backLeft = new(-x, 0f, -z);   // A
        Vector3 backRight = new(x, 0f, -z);   // B
        Vector3 frontRight = new(x, 0f, z);   // C
        Vector3 frontLeft = new(-x, 0f, z);   // D
        Vector3 topRight = new(x, height, z); // E
        Vector3 topLeft = new(-x, height, z); // F

        AddQuad(backLeft, backRight, frontRight, frontLeft, color);  // bottom  (-Y)
        AddQuad(backLeft, topLeft, topRight, backRight, color);     // slope   (+Y/-Z)
        AddQuad(frontLeft, frontRight, topRight, topLeft, color);    // back    (+Z)
        AddTriangle(backRight, topRight, frontRight, color);         // +X side
        AddTriangle(backLeft, frontLeft, topLeft, color);            // -X side
        return this;
    }

    /// <summary>Y-axis aligned cylinder centred on the origin (pillars, pads, projectiles).</summary>
    public MeshBuilder AddCylinder(float radius, float height, Color color, int segments = 16)
    {
        segments = Math.Max(3, segments);
        float halfHeight = height * 0.5f;
        Vector3 bottom = new(0f, -halfHeight, 0f);
        Vector3 top = new(0f, halfHeight, 0f);

        for (int i = 0; i < segments; i++)
        {
            float a0 = MathHelper.TwoPi * i / segments;
            float a1 = MathHelper.TwoPi * (i + 1) / segments;

            Vector3 b0 = new(MathF.Cos(a0) * radius, -halfHeight, MathF.Sin(a0) * radius);
            Vector3 b1 = new(MathF.Cos(a1) * radius, -halfHeight, MathF.Sin(a1) * radius);
            Vector3 t1 = new(b1.X, halfHeight, b1.Z);
            Vector3 t0 = new(b0.X, halfHeight, b0.Z);

            AddQuad(b0, t0, t1, b1, color);    // side
            AddTriangle(top, t1, t0, color);    // top cap (+Y)
            AddTriangle(bottom, b0, b1, color); // bottom cap (-Y)
        }

        return this;
    }
}
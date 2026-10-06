using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.Character;

/// <summary>
/// CPU linear-blend skinning.
///
/// This runs on the CPU on purpose. The project has no shader pipeline - no
/// <c>.fx</c>, no content builder, no <c>Effect</c> - only <see cref="BasicEffect"/>,
/// which cannot skin, and the graphics profile is Reach (shader model 3.0).
/// Adding a content pipeline to move 1880 vertices onto the GPU would be a
/// build-system change bought for a character this small, and would put a
/// hand-written vertex shader between the character and the lighting the rest of
/// the game already uses. So the bind pose is transformed here and uploaded
/// through the same vertex format as everything else.
///
/// The cost is 1880 vertices times four influences. The output is
/// <see cref="VertexPositionColorNormal"/> - the game's existing format - so the
/// character needs no new vertex declaration, no new material and no texture
/// sampling, and its four flat colours ride in the vertex colour.
///
/// Consequence to keep in mind: this is per-vertex CPU work, so it scales with
/// visible characters, not with triangles. That is fine for one player and a
/// handful of remote players; a crowd would want the GPU path, at which point
/// only <see cref="Skin"/> has to move, not the asset or the skeleton.
/// </summary>
public sealed class CharacterSkin
{
    private readonly CharacterAsset _asset;
    private readonly VertexPositionColorNormal[] _vertices;

    public CharacterSkin(CharacterAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        _asset = asset;
        _vertices = new VertexPositionColorNormal[asset.VertexCount];
    }

    /// <summary>Skinned output, in character-local space. Valid after the most recent <see cref="Skin"/>.</summary>
    public VertexPositionColorNormal[] Vertices => _vertices;

    /// <summary>Number of vertices written per skin call.</summary>
    public int VertexCount => _vertices.Length;

    /// <summary>
    /// Transforms the bind pose by <paramref name="skeleton"/> and leaves the result in
    /// <see cref="Vertices"/>.
    /// </summary>
    public void Skin(CharacterSkeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        if (skeleton.BoneCount != _asset.BoneCount)
        {
            throw new ArgumentException("Skeleton does not match the asset.", nameof(skeleton));
        }

        Matrix[] matrices = skeleton.Skinning;
        CharacterVertex[] source = _asset.Vertices;

        for (int i = 0; i < source.Length; i++)
        {
            CharacterVertex vertex = source[i];

            Vector3 position = Vector3.Transform(vertex.BindPosition, matrices[vertex.Joint0]) * vertex.Weight0;
            Vector3 normal = Vector3.TransformNormal(vertex.BindNormal, matrices[vertex.Joint0]) * vertex.Weight0;

            AddInfluence(ref position, ref normal, matrices, vertex.BindPosition, vertex.BindNormal,
                vertex.Joint1, vertex.Weight1);
            AddInfluence(ref position, ref normal, matrices, vertex.BindPosition, vertex.BindNormal,
                vertex.Joint2, vertex.Weight2);
            AddInfluence(ref position, ref normal, matrices, vertex.BindPosition, vertex.BindNormal,
                vertex.Joint3, vertex.Weight3);

            _vertices[i] = new VertexPositionColorNormal(
                position,
                vertex.Color,
                SafeNormalize(normal));
        }
    }

    /// <summary>
    /// Fills <paramref name="destination"/> with the bind pose, unskinned.
    ///
    /// Used by the headless checks to compare a skinned rest pose against the source
    /// geometry, and by the renderer to seed its buffers before the first animation tick.
    /// </summary>
    public void WriteBindPose(VertexPositionColorNormal[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (destination.Length < _asset.VertexCount)
        {
            throw new ArgumentException("Destination is smaller than the mesh.", nameof(destination));
        }

        CharacterVertex[] source = _asset.Vertices;
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = new VertexPositionColorNormal(source[i].BindPosition, source[i].Color, source[i].BindNormal);
        }
    }

    /// <summary>
    /// Folds one bone's contribution into the running position and normal. Linear
    /// blend skinning: every influence transforms the SAME bind-pose vertex and the
    /// results are summed by weight - the bind vertex must not be re-read per
    /// influence or the blend collapses toward whichever bone came last.
    /// </summary>
    private static void AddInfluence(ref Vector3 position, ref Vector3 normal, Matrix[] matrices,
        Vector3 bindPosition, Vector3 bindNormal, int joint, float weight)
    {
        if (weight <= 0f)
        {
            return;
        }

        position += Vector3.Transform(bindPosition, matrices[joint]) * weight;
        normal += Vector3.TransformNormal(bindNormal, matrices[joint]) * weight;
    }

    private static Vector3 SafeNormalize(Vector3 value)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared < 1e-8f)
        {
            return Vector3.UnitY;
        }

        return value / MathF.Sqrt(lengthSquared);
    }
}

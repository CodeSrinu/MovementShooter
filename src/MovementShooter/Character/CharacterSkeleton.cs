using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MovementShooter.Character;

/// <summary>
/// Evaluates a pose into per-bone skinning matrices.
///
/// Conventions matter here and getting them wrong produces a character that is
/// exploded, inside-out, or scaled to nothing, so they are stated once:
///
/// <list type="bullet">
/// <item>System.Numerics is row-vector: a point transforms as <c>p * M</c>, and in
/// <c>A * B</c> the A applies first. Translation therefore lives in the last ROW
/// (<c>M41..M43</c>), which is what <see cref="Matrix.CreateTranslation"/> produces.</item>
/// <item>The hierarchy composes as <c>global = local * parentGlobal</c> - a child's local
/// transform is expressed in its parent's space, so local applies first.</item>
/// <item>The converter emits each glTF inverse bind matrix already transposed into
/// row-vector form, so it loads straight into <see cref="Matrix"/> with no fixup.</item>
/// <item>A skinning matrix is <c>inverseBind * poseGlobal</c>. In bind pose that product is
/// exactly identity, which makes "skinning at rest changes nothing" a checkable property
/// rather than a hope.</item>
/// </list>
///
/// Everything is preallocated. Nothing here allocates per frame, because this runs for
/// every visible character every frame.
/// </summary>
public sealed class CharacterSkeleton
{
    private readonly int _boneCount;
    private readonly int[] _parents;
    private readonly int[] _order;
    private readonly Matrix[] _inverseBind;
    private readonly Matrix[] _bindGlobal;
    private readonly Matrix[] _bindLocal;
    private readonly Matrix[] _local;
    private readonly Matrix[] _global;
    private readonly Matrix[] _skinning;
    private readonly CharacterSocket[] _sockets;

    public CharacterSkeleton(CharacterAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        _boneCount = asset.BoneCount;
        _sockets = asset.Sockets;
        _parents = new int[_boneCount];
        _inverseBind = new Matrix[_boneCount];
        _bindGlobal = new Matrix[_boneCount];
        _bindLocal = new Matrix[_boneCount];
        _local = new Matrix[_boneCount];
        _global = new Matrix[_boneCount];
        _skinning = new Matrix[_boneCount];

        for (int i = 0; i < _boneCount; i++)
        {
            _inverseBind[i] = asset.Bones[i].InverseBind;
            _parents[i] = asset.Bones[i].ParentIndex;
        }

        // Parents before children, derived from the hierarchy rather than from bone
        // order. A re-exported rig reorders bones, and trusting the order would compose
        // children against stale parent transforms and shear the mesh.
        _order = ResolveBindOrder(_parents, asset);

        // A joint's bind global transform is the inverse of its inverse bind matrix.
        // This MonoGame build's Invert returns void, so singularity is checked with the
        // determinant first - a silently singular bone produces garbage skinning matrices
        // that look like a corrupted mesh rather than reporting an error.
        for (int i = 0; i < _boneCount; i++)
        {
            if (MathF.Abs(_inverseBind[i].Determinant()) < 1e-9f)
            {
                throw new ArgumentException(
                    "Bone '" + asset.Bones[i].Name + "' has a singular inverse bind matrix.", nameof(asset));
            }

            Matrix source = _inverseBind[i];
            Matrix inverted;
            Matrix.Invert(ref source, out inverted);
            _bindGlobal[i] = inverted;
        }

        for (int index = 0; index < _boneCount; index++)
        {
            int bone = _order[index];
            int parent = _parents[bone];

            if (parent < 0)
            {
                _bindLocal[bone] = _bindGlobal[bone];
            }
            else
            {
                if (MathF.Abs(_bindGlobal[parent].Determinant()) < 1e-9f)
                {
                    throw new ArgumentException(
                        "Bone '" + asset.Bones[bone].Name + "' has a singular parent transform.", nameof(asset));
                }

                Matrix parentGlobal = _bindGlobal[parent];
                Matrix inverted;
                Matrix.Invert(ref parentGlobal, out inverted);

                // global = local * parentGlobal, so local = global * inverse(parentGlobal)
                _bindLocal[bone] = _bindGlobal[bone] * inverted;
            }
        }

        Reset();
    }

    public int BoneCount => _boneCount;

    /// <summary>Per-bone matrices taking a bind-pose vertex to the posed character.</summary>
    public Matrix[] Skinning => _skinning;

    /// <summary>Bind-pose local transform, for comparing a clip's first frame against rest.</summary>
    public Matrix BindLocal(int bone) => _bindLocal[bone];

    /// <summary>Bind-pose global transform.</summary>
    public Matrix BindGlobal(int bone) => _bindGlobal[bone];

    /// <summary>
    /// The posed global transform of one bone.
    ///
    /// Only meaningful after <see cref="Compose"/>. Valid during cross-fades because
    /// cross-fading blends local transforms before composing, so there is still exactly
    /// one posed hierarchy rather than two.
    /// </summary>
    public Matrix PoseGlobal(int bone) => _global[bone];

    /// <summary>
    /// Resolves an attachment point's world transform from the current pose.
    ///
    /// Sockets are not bones: they are Blender Empties parented to bones, carrying no
    /// skin weights and never part of the skinning hierarchy. They therefore compose
    /// alongside the skeleton rather than inside it - a socket's offset is expressed in
    /// its parent's space and applied on top of whatever that parent is currently doing.
    ///
    /// A socket parented to another socket composes through it, so a muzzle point hung
    /// off a weapon socket follows the weapon and not the hand.
    /// </summary>
    public Matrix SocketWorld(int socket)
    {
        CharacterSocket s = _sockets[socket];

        Matrix parentGlobal;
        if (s.ParentSocket >= 0)
        {
            parentGlobal = SocketWorld(s.ParentSocket);
        }
        else if (s.ParentBone >= 0)
        {
            parentGlobal = _global[s.ParentBone];
        }
        else
        {
            parentGlobal = Matrix.Identity;
        }

        // Row-vector convention: the local offset applies first, in the parent's space.
        return s.Local * parentGlobal;
    }

    /// <summary>Returns every bone to bind and rebuilds the skinning matrices.</summary>
    public void Reset()
    {
        for (int i = 0; i < _boneCount; i++)
        {
            _local[i] = _bindLocal[i];
        }

        Compose();
    }

    /// <summary>Overwrites one bone's local transform. Call <see cref="Compose"/> once after a batch.</summary>
    public void SetLocal(int bone, Matrix local)
    {
        if ((uint)bone >= (uint)_boneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bone));
        }

        _local[bone] = local;
    }

    /// <summary>Copies another skeleton's local transforms, for cross-fading.</summary>
    public void CopyLocalFrom(CharacterSkeleton other)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameSkeleton(other);

        Array.Copy(other._local, _local, _boneCount);
    }

    /// <summary>Blends this skeleton's local transforms toward <paramref name="other"/>.</summary>
    public void BlendLocalFrom(CharacterSkeleton other, float amount)
    {
        ArgumentNullException.ThrowIfNull(other);
        RequireSameSkeleton(other);

        float t = Math.Clamp(amount, 0f, 1f);
        for (int i = 0; i < _boneCount; i++)
        {
            _local[i] = Matrix.Lerp(_local[i], other._local[i], t);
        }
    }

    /// <summary>Recomputes global and skinning matrices from the current local transforms.</summary>
    public void Compose()
    {
        for (int index = 0; index < _order.Length; index++)
        {
            int bone = _order[index];
            int parent = _parents[bone];

            _global[bone] = parent < 0 ? _local[bone] : _local[bone] * _global[parent];
            _skinning[bone] = _inverseBind[bone] * _global[bone];
        }
    }

    private void RequireSameSkeleton(CharacterSkeleton other)
    {
        if (other.BoneCount != _boneCount)
        {
            throw new ArgumentException(
                "Cannot blend skeletons with " + other.BoneCount + " and " + _boneCount + " bones.", nameof(other));
        }
    }

    /// <summary>
    /// Orders bones so every parent precedes its children, by walking each bone up to
    /// its root and emitting that chain root-first.
    /// </summary>
    private static int[] ResolveBindOrder(int[] parents, CharacterAsset asset)
    {
        int count = parents.Length;
        List<int> order = new(count);
        bool[] emitted = new bool[count];
        List<int> chain = new();

        for (int start = 0; start < count; start++)
        {
            if (emitted[start])
            {
                continue;
            }

            chain.Clear();
            int current = start;
            while (current >= 0)
            {
                if (chain.Count > count)
                {
                    throw new ArgumentException("Bone hierarchy contains a cycle at '" +
                                                 asset.Bones[start].Name + "'.", nameof(asset));
                }

                chain.Add(current);
                current = parents[current];
            }

            // chain runs child -> root, so emit it backwards.
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (!emitted[chain[i]])
                {
                    emitted[chain[i]] = true;
                    order.Add(chain[i]);
                }
            }
        }

        return order.ToArray();
    }
}

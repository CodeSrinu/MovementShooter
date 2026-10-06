using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace KineticAssetTool;

/// <summary>
/// Converts the KINETIC character from .glb into the runtime asset blob.
///
/// Exit codes: 0 success, 1 bad arguments, 2 conversion refused.
///
/// Everything is validated before a single byte is written. A converter that
/// emits a plausible-looking asset from a subtly wrong input is worse than one
/// that refuses, because the symptom shows up as a character with a collapsed
/// limb several systems away from here.
/// </summary>
internal static class Program
{
    private const float WeightTolerance = 0.02f;
    private const float ScaleTolerance = 1e-4f;

    private static int Main(string[] args)
    {
        string? input = null;
        string? output = null;
        string? muzzle = null;
        bool weapon = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" when i + 1 < args.Length:
                    input = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--weapon":
                    weapon = true;
                    break;
                case "--muzzle" when i + 1 < args.Length:
                    muzzle = args[++i];
                    break;
                case "--help" or "-h":
                    Console.WriteLine("KineticAssetTool --input <file.glb> --output <file.bin> [--weapon]");
                    Console.WriteLine("KineticAssetTool --input <file.glb> --output <file.bin> --weapon --muzzle x,y,z");
                    return 0;
                default:
                    Console.Error.WriteLine("Unrecognised argument: " + args[i]);
                    return 1;
            }
        }

        if (input is null || output is null)
        {
            Console.Error.WriteLine("Both --input and --output are required.");
            return 1;
        }

        try
        {
            if (weapon)
            {
                ConvertWeapon(input, output, muzzle);
            }
            else
            {
                Convert(input, output);
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException or IOException)
        {
            Console.Error.WriteLine("Conversion refused: " + ex.Message);
            return 2;
        }
    }

    private static void Convert(string inputPath, string outputPath)
    {
        GlbDocument glb = GlbDocument.Load(inputPath);
        Console.WriteLine("read " + Path.GetFileName(inputPath));

        if (glb.BoneNames.Count == 0)
        {
            throw new InvalidDataException("No skin found.");
        }

        if (glb.MeshNode < 0)
        {
            throw new InvalidDataException("No skinned mesh node found.");
        }

        int boneCount = glb.BoneNames.Count;
        ConvertedAsset asset = new();

        for (int i = 0; i < boneCount; i++)
        {
            asset.BoneNames.Add(glb.BoneNames[i]);
            asset.BoneParents.Add(glb.BoneParents[i]);
            asset.InverseBinds.Add(glb.InverseBinds.Count > i ? glb.InverseBinds[i] : Identity());
        }

        // --- attachment points ---
        // A socket is authored as an Empty parented to a bone, so glTF carries it as a
        // plain child node. Its parent is resolved to either a bone or another socket,
        // and the offset is stored in that parent's space. Sockets are never added to
        // the bone list: they carry no skin weights, and treating them as bones would
        // change the skeleton's shape and every index derived from it.
        Dictionary<int, int> nodeToBone = new();
        for (int bone = 0; bone < boneCount; bone++)
        {
            for (int node = 0; node < glb.Nodes.Count; node++)
            {
                if (glb.Nodes[node].Name == asset.BoneNames[bone])
                {
                    nodeToBone[node] = bone;
                    break;
                }
            }
        }

        // glTF does not guarantee that a parent node precedes its children, and MuzzlePoint
        // hangs off WeaponSocket, so parents are resolved by walking the chain rather
        // than by assuming the order sockets were discovered in.
        Dictionary<int, int> socketByNode = new();
        for (int i = 0; i < glb.Sockets.Count; i++)
        {
            socketByNode[glb.Sockets[i].Node] = i;
        }

        Dictionary<int, (int Bone, int Socket)> resolved = new();

        (int Bone, int Socket) ResolveParent(int socketIndex)
        {
            if (resolved.TryGetValue(socketIndex, out (int Bone, int Socket) known))
            {
                return known;
            }

            GlbSocket socket = glb.Sockets[socketIndex];
            int parentNode = socket.ParentNode;

            // A socket parented to another socket must stay parented to that socket, not
            // collapse onto the bone behind it. MuzzlePoint hangs off WeaponSocket, and
            // resolving it to Hand.R would place the muzzle in the palm of the hand and
            // silently discard the weapon's own offset.
            // The answer names the *immediate* attachment, not the bone at the far end of the
            // chain. Returning the ancestor bone here would flatten MuzzlePoint onto
            // Hand.R and silently drop WeaponSocket's own offset, putting the muzzle in
            // the palm of the hand. The chain is walked at runtime instead.
            if (socketByNode.TryGetValue(parentNode, out int parentSocket))
            {
                _ = ResolveParent(parentSocket); // validate the rest of the chain
                (int Bone, int Socket) answer = (-1, parentSocket);
                resolved[socketIndex] = answer;
                return answer;
            }

            if (nodeToBone.TryGetValue(parentNode, out int parentBone))
            {
                (int Bone, int Socket) answer = (parentBone, -1);
                resolved[socketIndex] = answer;
                return answer;
            }

            throw new InvalidDataException(
                "Socket '" + socket.Name + "' is attached to a node that is neither a bone nor a " +
                "socket; an attachment point with no resolvable parent cannot be placed on the character.");
        }

        for (int i = 0; i < glb.Sockets.Count; i++)
        {
            _ = ResolveParent(i);
        }

        for (int i = 0; i < glb.Sockets.Count; i++)
        {
            // Emitted in discovery order, so a socket may reference another that has not been
            // written yet. The ancestor bone is therefore read from the source list, never
            // from the partially built asset.
            GlbSocket socket = glb.Sockets[i];
            (int Bone, int Socket) parent = resolved[i];

            int ancestorBone = parent.Bone;
            if (parent.Socket >= 0)
            {
                ancestorBone = resolved[parent.Socket].Bone;
            }

            asset.Sockets.Add(new ConvertedSocket
            {
                Name = socket.Name,
                ParentBone = ancestorBone,
                ParentSocket = parent.Socket,
                Local = ComposeLocal(socket.Translation, socket.Rotation, socket.Scale),
            });
        }

        // --- mesh: primitives are concatenated into one vertex/index buffer ---
        // The runtime draws submesh ranges out of a single pair of buffers, which
        // keeps it to one vertex upload for the whole character.
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        int vertexBase = 0;
        int indexBase = 0;

        foreach (GlbPrimitive primitive in glb.Primitives)
        {
            if (primitive.JointAccessor is null || primitive.WeightAccessor is null)
            {
                throw new InvalidDataException("A primitive has no skin weights; the character must be fully skinned.");
            }

            float[] positions = glb.ReadFloats(primitive.PositionAccessor);
            float[] normals = primitive.NormalAccessor >= 0 ? glb.ReadFloats(primitive.NormalAccessor) : new float[positions.Length];
            float[] weights = glb.ReadFloats(primitive.WeightAccessor.Value);
            float[] jointsRaw = glb.ReadFloats(primitive.JointAccessor.Value);
            int[] indices = glb.ReadIndices(primitive.IndexAccessor);

            int vertexCount = positions.Length / 3;
            int count = weights.Length / 4;

            if (count != vertexCount)
            {
                throw new InvalidDataException("JOINTS_0 and POSITION disagree on vertex count.");
            }

            ConvertedMaterial material = MaterialFor(glb, primitive.Material);

            for (int v = 0; v < vertexCount; v++)
            {
                float w0 = weights[(v * 4) + 0];
                float w1 = weights[(v * 4) + 1];
                float w2 = weights[(v * 4) + 2];
                float w3 = weights[(v * 4) + 3];
                float total = w0 + w1 + w2 + w3;

                if (total < 1e-4f)
                {
                    throw new InvalidDataException("Vertex " + v.ToString(CultureInfo.InvariantCulture) +
                                                   " has no skin influence; it would collapse to the origin when skinned.");
                }

                if (MathF.Abs(total - 1f) > WeightTolerance)
                {
                    throw new InvalidDataException("Vertex " + v.ToString(CultureInfo.InvariantCulture) +
                                                   " skin weights sum to " + total.ToString("0.0000", CultureInfo.InvariantCulture) +
                                                   ", not 1.");
                }

                int j0 = (int)jointsRaw[(v * 4) + 0];
                int j1 = (int)jointsRaw[(v * 4) + 1];
                int j2 = (int)jointsRaw[(v * 4) + 2];
                int j3 = (int)jointsRaw[(v * 4) + 3];

                foreach (int joint in new[] { j0, j1, j2, j3 })
                {
                    if (joint < 0 || joint >= boneCount)
                    {
                        throw new InvalidDataException("Vertex references bone " +
                                                       joint.ToString(CultureInfo.InvariantCulture) + " but the skin has " +
                                                       boneCount.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                }

                float px = positions[(v * 3) + 0];
                float py = positions[(v * 3) + 1];
                float pz = positions[(v * 3) + 2];
                minY = MathF.Min(minY, py);
                maxY = MathF.Max(maxY, py);

                asset.Vertices.Add(new ConvertedVertex
                {
                    Px = px, Py = py, Pz = pz,
                    Nx = normals[(v * 3) + 0], Ny = normals[(v * 3) + 1], Nz = normals[(v * 3) + 2],
                    J0 = j0, J1 = j1, J2 = j2, J3 = j3,
                    W0 = w0, W1 = w1, W2 = w2, W3 = w3,
                    R = ToByte(material.R), G = ToByte(material.G),
                    B = ToByte(material.B), A = 255,
                });
            }

            foreach (int index in indices)
            {
                asset.Indices.Add(index + vertexBase);
            }

            asset.Submeshes.Add(new ConvertedSubmesh(indexBase, indices.Length, primitive.Material));
            vertexBase += vertexCount;
            indexBase += indices.Length;
        }

        for (int i = 0; i < glb.Materials.Count; i++)
        {
            ConvertedMaterial material = MaterialFor(glb, i);
            asset.Materials.Add(material);
        }

        asset.MinY = minY;
        asset.MaxY = maxY;

        // --- animations ---
        // Animations address glTF nodes; the runtime addresses bones. Map between
        // them by matching bone names back to the node list, so the two orderings
        // (skin.joints versus nodes) cannot silently disagree.
        foreach (GlbAnimation animation in glb.Animations)
        {
            asset.Clips.Add(ConvertClip(animation, nodeToBone, boneCount, out string? problem));
            if (problem is not null)
            {
                throw new InvalidDataException(problem);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        CharacterAssetWriter.Write(outputPath, asset);

        Console.Write(CharacterAssetWriter.Describe(asset));
        foreach (ConvertedClip clip in asset.Clips)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  clip {0,-16} {1,4} keys  {2:0.000} s", clip.Name, clip.Times.Length, clip.Duration));
        }

        FileInfo info = new(outputPath);
        Console.WriteLine("wrote " + info.FullName + " (" +
                          info.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes)");
    }

    /// <summary>
    /// Converts a rigid weapon: one mesh, per-material primitives, no skin, no animation.
    ///
    /// A weapon is a separate asset from the character on purpose. It carries no skin
    /// weights and is never blended, so demanding a skeleton here would invent a rig
    /// nobody uses. What it does carry is an authored muzzle position, supplied by
    /// <c>--muzzle x,y,z</c> in weapon space - never derived from the mesh bounds, since
    /// a launcher whose muzzle bell flares wider than its bore would report the bell's
    /// outer edge as the muzzle.
    /// </summary>
    private static void ConvertWeapon(string inputPath, string outputPath, string? muzzleArgument)
    {
        GlbDocument glb = GlbDocument.Load(inputPath);
        Console.WriteLine("read " + Path.GetFileName(inputPath) + " as a weapon");

        if (glb.Primitives.Count == 0)
        {
            throw new InvalidDataException("Weapon has no primitives.");
        }

        ConvertedWeapon weapon = new() { Name = Path.GetFileNameWithoutExtension(inputPath) };

        if (muzzleArgument is not null)
        {
            string[] parts = muzzleArgument.Split(',');
            if (parts.Length != 3 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float my) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float mz))
            {
                throw new InvalidDataException(
                    "--muzzle expects three comma-separated numbers, for example --muzzle 0,0,-0.62.");
            }

            weapon.Muzzle = new[] { mx, my, mz };
            weapon.HasMuzzle = true;
        }

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        int vertexBase = 0;
        int indexBase = 0;

        foreach (GlbPrimitive primitive in glb.Primitives)
        {
            float[] positions = glb.ReadFloats(primitive.PositionAccessor);
            float[] normals = primitive.NormalAccessor >= 0
                ? glb.ReadFloats(primitive.NormalAccessor)
                : new float[positions.Length];
            int[] indices = glb.ReadIndices(primitive.IndexAccessor);

            int vertexCount = positions.Length / 3;
            if (normals.Length != positions.Length)
            {
                throw new InvalidDataException("POSITION and NORMAL disagree on vertex count; the weapon must be flat shaded.");
            }

            if (indices.Length % 3 != 0)
            {
                throw new InvalidDataException("Weapon index count is not a multiple of three.");
            }

            ConvertedMaterial material = MaterialFor(glb, primitive.Material);

            for (int v = 0; v < vertexCount; v++)
            {
                float px = positions[(v * 3) + 0];
                float py = positions[(v * 3) + 1];
                float pz = positions[(v * 3) + 2];

                // A zero-length normal shades as black, which reads as a hole in the mesh
                // rather than as a bad normal, so it is refused here.
                float nx = normals[(v * 3) + 0];
                float ny = normals[(v * 3) + 1];
                float nz = normals[(v * 3) + 2];
                float length = MathF.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
                if (length < 1e-4f)
                {
                    throw new InvalidDataException("Weapon vertex " + v.ToString(CultureInfo.InvariantCulture) +
                                                   " has a zero-length normal.");
                }

                minY = MathF.Min(minY, py);
                maxY = MathF.Max(maxY, py);

                weapon.Vertices.Add(new ConvertedVertex
                {
                    Px = px, Py = py, Pz = pz,
                    Nx = nx / length, Ny = ny / length, Nz = nz / length,
                    R = ToByte(material.R), G = ToByte(material.G),
                    B = ToByte(material.B), A = 255,
                });
            }

            foreach (int index in indices)
            {
                weapon.Indices.Add(index + vertexBase);
            }

            weapon.Submeshes.Add(new ConvertedSubmesh(indexBase, indices.Length, primitive.Material));
            vertexBase += vertexCount;
            indexBase += indices.Length;
        }

        for (int i = 0; i < glb.Materials.Count; i++)
        {
            weapon.Materials.Add(MaterialFor(glb, i));
        }

        weapon.MinY = minY;
        weapon.MaxY = maxY;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        WeaponAssetWriter.Write(outputPath, weapon);

        Console.Write(WeaponAssetWriter.Describe(weapon));

        FileInfo info = new(outputPath);
        Console.WriteLine("wrote " + info.FullName + " (" +
                          info.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes)");
    }

    private static ConvertedClip ConvertClip(GlbAnimation animation, Dictionary<int, int> nodeToBone,
        int boneCount, out string? problem)
    {
        problem = null;

        // Union of every key time across the clip becomes the sampling grid.
        SortedSet<float> times = new();
        foreach (GlbChannel channel in animation.Channels)
        {
            foreach (float time in channel.Times)
            {
                // Quantise before inserting: separate channels report the same
                // instant as floats that differ in the last bits, and without
                // this the union grid picks up phantom near-duplicate frames.
                times.Add(MathF.Round(time, 5));
            }
        }

        if (times.Count == 0)
        {
            problem = "Clip " + animation.Name + " has no keys.";
            return new ConvertedClip { Name = animation.Name };
        }

        float[] grid = new float[times.Count];
        times.CopyTo(grid);

        // Blender's exporter maps frame 1 to 1/fps rather than 0, so the grid arrives
        // offset by one frame. Normalise it: a clip whose first key is not at zero
        // means time 0 clamps to the first pose and the loop seam lands a frame late.
        float origin = grid[0];
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] -= origin;
        }

        // Per bone, per path sampler lookup.
        Dictionary<int, Dictionary<string, GlbChannel>> byBone = new();
        foreach (GlbChannel channel in animation.Channels)
        {
            if (!nodeToBone.TryGetValue(channel.Node, out int bone))
            {
                continue; // an animated node that is not a joint; not ours to play
            }

            if (!byBone.TryGetValue(bone, out Dictionary<string, GlbChannel>? paths))
            {
                paths = new Dictionary<string, GlbChannel>();
                byBone[bone] = paths;
            }

            paths[channel.Path] = channel;
        }

        float[] translation = new float[grid.Length * boneCount * 3];
        float[] rotation = new float[grid.Length * boneCount * 4];

        for (int bone = 0; bone < boneCount; bone++)
        {
            // Look the three channels up separately. Chaining them with && and an
            // out var leaves the variable "possibly unassigned" when the chain
            // short-circuits, and an absent channel has to fall back to identity
            // rather than to a stale reference.
            GlbChannel? translationChannel = ChannelFor(byBone, bone, "translation");
            GlbChannel? rotationChannel = ChannelFor(byBone, bone, "rotation");
            GlbChannel? scaleChannel = ChannelFor(byBone, bone, "scale");

            if (scaleChannel is not null)
            {
                for (int c = 0; c + 2 < scaleChannel.Values.Length; c += 3)
                {
                    if (MathF.Abs(scaleChannel.Values[c] - 1f) > ScaleTolerance ||
                        MathF.Abs(scaleChannel.Values[c + 1] - 1f) > ScaleTolerance ||
                        MathF.Abs(scaleChannel.Values[c + 2] - 1f) > ScaleTolerance)
                    {
                        problem = "Clip " + animation.Name + " bone " + bone.ToString(CultureInfo.InvariantCulture) +
                                  " has non-identity scale, which this asset format does not store.";
                        return new ConvertedClip { Name = animation.Name };
                    }
                }
            }

            for (int f = 0; f < grid.Length; f++)
            {
                float time = grid[f];
                float[] t3 = translationChannel is not null ? Sample(translationChannel, time) : new float[] { 0f, 0f, 0f };
                float[] r4 = rotationChannel is not null ? Sample(rotationChannel, time) : new float[] { 0f, 0f, 0f, 1f };

                int tBase = ((f * boneCount) + bone) * 3;
                translation[tBase + 0] = t3[0];
                translation[tBase + 1] = t3[1];
                translation[tBase + 2] = t3[2];

                int rBase = ((f * boneCount) + bone) * 4;
                rotation[rBase + 0] = r4[0];
                rotation[rBase + 1] = r4[1];
                rotation[rBase + 2] = r4[2];
                rotation[rBase + 3] = r4[3];
            }
        }

        float duration = 0f;
        foreach (float time in grid)
        {
            duration = MathF.Max(duration, time);
        }

        return new ConvertedClip
        {
            Name = animation.Name,
            Times = grid,
            Translation = translation,
            Rotation = rotation,
            Duration = duration,
        };
    }

    private static GlbChannel? ChannelFor(Dictionary<int, Dictionary<string, GlbChannel>> byBone, int bone, string path)
    {
        return byBone.TryGetValue(bone, out Dictionary<string, GlbChannel>? paths) &&
               paths.TryGetValue(path, out GlbChannel? channel)
            ? channel
            : null;
    }

    /// <summary>Samples a channel at a time, linearly between the bracketing keys.</summary>
    private static float[] Sample(GlbChannel channel, float time)
    {
        int stride = channel.ComponentsPerKey;
        float[] times = channel.Times;
        float[] values = channel.Values;

        if (times.Length == 0)
        {
            return new float[stride];
        }

        if (time <= times[0])
        {
            return Slice(values, 0, stride);
        }

        for (int i = 1; i < times.Length; i++)
        {
            if (time > times[i])
            {
                continue;
            }

            float span = times[i] - times[i - 1];
            float t = span <= 1e-6f ? 0f : (time - times[i - 1]) / span;
            float[] a = Slice(values, (i - 1) * stride, stride);
            float[] b = Slice(values, i * stride, stride);
            float[] result = new float[stride];
            for (int c = 0; c < stride; c++)
            {
                result[c] = a[c] + ((b[c] - a[c]) * t);
            }

            return result;
        }

        return Slice(values, (times.Length - 1) * stride, stride);
    }

    private static float[] Slice(float[] source, int start, int length)
    {
        float[] result = new float[length];
        Array.Copy(source, start, result, 0, length);
        return result;
    }

    private static ConvertedMaterial MaterialFor(GlbDocument glb, int index)
    {
        if (index < 0 || index >= glb.Materials.Count)
        {
            throw new InvalidDataException("Primitive references material " +
                                           index.ToString(CultureInfo.InvariantCulture) +
                                           " but the file has " +
                                           glb.Materials.Count.ToString(CultureInfo.InvariantCulture) + ".");
        }

        GlbMaterial material = glb.Materials[index];

        // glTF base colour is linear; the renderer shades in the same space, so
        // no gamma conversion here. Converting would double-darken the palette.
        return new ConvertedMaterial(material.Name,
            material.BaseColorFactor[0], material.BaseColorFactor[1], material.BaseColorFactor[2],
            material.RoughnessFactor, material.MetallicFactor);
    }

    private static byte ToByte(float linear)
    {
        float clamped = Math.Clamp(linear, 0f, 1f);
        return (byte)MathF.Round(clamped * 255f);
    }

    private static float[] Identity()
    {
        return new float[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f };
    }

    /// <summary>
    /// Composes a node's TRS into a single row-vector matrix, in the same convention the
    /// runtime's bones use: translation in the last row, row-vector multiply order.
    ///
    /// glTF composes as scale, then rotate, then translate applied to the vector, which in
    /// row-vector form is <c>Scale * Rotation * Translation</c> - the same order
    /// <see cref="CharacterSkeleton"/> composes bone locals in.
    /// </summary>
    private static float[] ComposeLocal(float[] translation, float[] rotation, float[] scale)
    {
        float x = rotation[0], y = rotation[1], z = rotation[2], w = rotation[3];

        // Standard quaternion to rotation matrix, then transposed into row-vector form:
        // System.Numerics multiplies p * M, so the transpose of the column-vector result
        // is what a point needs here.
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;
        float wx = w * x, wy = w * y, wz = w * z;

        float[] r =
        {
            1f - (2f * (yy + zz)), 2f * (xy + wz),       2f * (xz - wy),       0f,
            2f * (xy - wz),       1f - (2f * (xx + zz)),  2f * (yz + wx),       0f,
            2f * (xz + wy),       2f * (yz - wx),        1f - (2f * (xx + yy)), 0f,
            0f,                   0f,                    0f,                   1f,
        };

        for (int column = 0; column < 3; column++)
        {
            r[column * 4 + 0] *= scale[0];
            r[(column * 4) + 1] *= scale[1];
            r[(column * 4) + 2] *= scale[2];
        }

        float[] result = new float[16];
        for (int i = 0; i < 16; i++)
        {
            result[i] = r[i];
        }

        result[12] = translation[0];
        result[13] = translation[1];
        result[14] = translation[2];
        return result;
    }
}

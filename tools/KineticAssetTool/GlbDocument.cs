using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KineticAssetTool;

/// <summary>Component types a glTF accessor may use.</summary>
internal enum GlbComponent
{
    Byte = 5120,
    UnsignedByte = 5121,
    Short = 5122,
    UnsignedShort = 5123,
    UnsignedInt = 5125,
    Float = 5126,
}

/// <summary>Shape of a glTF accessor.</summary>
internal enum GlbShape
{
    Scalar,
    Vec2,
    Vec3,
    Vec4,
    Mat4,
}

/// <summary>A single node's local transform, in glTF order.</summary>
internal sealed class GlbNode
{
    public string Name { get; init; } = string.Empty;
    public float[] Translation { get; init; } = { 0f, 0f, 0f };
    public float[] Rotation { get; init; } = { 0f, 0f, 0f, 1f };
    public float[] Scale { get; init; } = { 1f, 1f, 1f };
    public int? Mesh { get; init; }
    public int? Skin { get; init; }
    public List<int> Children { get; } = new();

    /// <summary>Parent node index, or -1 for a scene root. glTF has no parent field, so this is derived.</summary>
    public int Parent { get; set; } = -1;
}

/// <summary>
/// An attachment point: a node carrying a name and a transform but no geometry.
///
/// The character's weapon sockets are Blender Empties parented to bones, not bones.
/// glTF has no concept of either, so they can only survive as ordinary child nodes
/// hanging off the joint they are attached to. That is what this represents.
/// </summary>
internal sealed class GlbSocket
{
    public string Name { get; init; } = string.Empty;

    /// <summary>This socket's own glTF node index.</summary>
    public int Node { get; init; } = -1;

    /// <summary>Index of the parent glTF node. May be a joint or another socket.</summary>
    public int ParentNode { get; init; } = -1;

    public float[] Translation { get; init; } = { 0f, 0f, 0f };
    public float[] Rotation { get; init; } = { 0f, 0f, 0f, 1f };
    public float[] Scale { get; init; } = { 1f, 1f, 1f };
}

/// <summary>One node's animation track for a single property.</summary>
internal sealed class GlbChannel
{
    public int Node { get; init; }
    public string Path { get; init; } = string.Empty;
    public float[] Times { get; init; } = Array.Empty<float>();
    public float[] Values { get; init; } = Array.Empty<float>();
    public int ComponentsPerKey { get; init; }
}

/// <summary>One animation: a named bag of channels.</summary>
internal sealed class GlbAnimation
{
    public string Name { get; init; } = string.Empty;
    public List<GlbChannel> Channels { get; } = new();
}

/// <summary>One mesh primitive: an accessor's worth of geometry plus a material.</summary>
internal sealed class GlbPrimitive
{
    public int PositionAccessor { get; init; }
    public int NormalAccessor { get; init; }
    public int? JointAccessor { get; init; }
    public int? WeightAccessor { get; init; }
    public int IndexAccessor { get; init; }
    public int Material { get; init; }
}

/// <summary>Material base colour and PBR scalars, as authored.</summary>
internal sealed class GlbMaterial
{
    public string Name { get; init; } = string.Empty;
    public float[] BaseColorFactor { get; init; } = { 1f, 1f, 1f, 1f };
    public float RoughnessFactor { get; init; } = 1f;
    public float MetallicFactor { get; init; } = 0f;
}

/// <summary>
/// A minimal glTF 2.0 binary (.glb) reader, scoped to exactly what the KINETIC
/// character uses: one skinned mesh split into per-material primitives, one
/// skin, and N animations of node TRS.
///
/// Supports what that needs and refuses the rest loudly rather than guessing:
/// sparse accessors, string-typed buffer views and non-triangle modes are
/// rejected, because silently mis-reading any of them would produce a plausible
/// looking but wrong asset.
/// </summary>
internal sealed class GlbDocument
{
    private const uint GlbMagic = 0x46546C67; // 'glTF'
    private const uint ChunkJson = 0x4E4F534A; // 'JSON'
    private const uint ChunkBin = 0x004E4942;  // 'BIN\0'

    private readonly byte[] _binary;
    private readonly JsonElement[] _accessors;
    private readonly JsonElement[] _bufferViews;

    public List<GlbNode> Nodes { get; } = new();
    public List<int> SceneRoots { get; } = new();
    public List<GlbPrimitive> Primitives { get; } = new();
    public List<GlbMaterial> Materials { get; } = new();
    public List<string> BoneNames { get; } = new();
    public List<int> BoneParents { get; } = new();
    public List<float[]> InverseBinds { get; } = new();
    public List<GlbAnimation> Animations { get; } = new();

    /// <summary>
    /// Attachment points found in the node graph: named, geometry-free nodes that hang
    /// off the skeleton. Empty for a character exported without its weapon sockets.
    /// </summary>
    public List<GlbSocket> Sockets { get; } = new();

    public int MeshNode { get; private set; } = -1;

    /// <summary>Indices of the nodes that are joints of the skin, in skin.joints order.</summary>
    public List<int> JointNodes { get; } = new();

    private GlbDocument(byte[] binary, JsonElement[] accessors, JsonElement[] bufferViews)
    {
        _binary = binary;
        _accessors = accessors;
        _bufferViews = bufferViews;
    }

    public static GlbDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 12)
        {
            throw new InvalidDataException("File is too small to be a GLB.");
        }

        uint magic = BitConverter.ToUInt32(bytes, 0);
        if (magic != GlbMagic)
        {
            throw new InvalidDataException("Not a GLB: bad magic 0x" + magic.ToString("X8", CultureInfo.InvariantCulture) + ".");
        }

        uint version = BitConverter.ToUInt32(bytes, 4);
        if (version != 2)
        {
            throw new InvalidDataException("Expected glTF binary version 2, got " + version.ToString(CultureInfo.InvariantCulture) + ".");
        }

        uint declared = BitConverter.ToUInt32(bytes, 8);
        if (declared != bytes.Length)
        {
            throw new InvalidDataException("Header length " + declared.ToString(CultureInfo.InvariantCulture) +
                                           " does not match the file size " + bytes.Length.ToString(CultureInfo.InvariantCulture) + ".");
        }

        byte[] binary = Array.Empty<byte>();
        JsonElement? root = null;

        int offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            uint length = BitConverter.ToUInt32(bytes, offset);
            uint kind = BitConverter.ToUInt32(bytes, offset + 4);
            offset += 8;
            if (offset + (int)length > bytes.Length)
            {
                throw new InvalidDataException("Chunk extends past the end of the file.");
            }

            if (kind == ChunkJson)
            {
                root = JsonDocument.Parse(Encoding.UTF8.GetString(bytes, offset, (int)length)).RootElement.Clone();
            }
            else if (kind == ChunkBin)
            {
                binary = new byte[length];
                Buffer.BlockCopy(bytes, offset, binary, 0, (int)length);
            }

            offset += (int)length;
        }

        if (root is null)
        {
            throw new InvalidDataException("GLB has no JSON chunk.");
        }

        JsonElement doc = root.Value;

        static JsonElement[] ToArray(JsonElement.ArrayEnumerator enumerator)
        {
            List<JsonElement> items = new();
            foreach (JsonElement item in enumerator)
            {
                items.Add(item);
            }

            return items.ToArray();
        }

        JsonElement[] accessors = doc.TryGetProperty("accessors", out JsonElement acc) ? ToArray(acc.EnumerateArray()) : Array.Empty<JsonElement>();
        JsonElement[] views = doc.TryGetProperty("bufferViews", out JsonElement bv) ? ToArray(bv.EnumerateArray()) : Array.Empty<JsonElement>();

        GlbDocument result = new(binary, accessors, views);
        result.Parse(doc);
        return result;
    }

    private void Parse(JsonElement doc)
    {
        List<GlbNode> nodes = new();
        foreach (JsonElement element in doc.GetProperty("nodes").EnumerateArray())
        {
            GlbNode node = new()
            {
                Name = Str(element, "name"),
                Translation = Floats(element, "translation", new float[] { 0f, 0f, 0f }),
                Rotation = Floats(element, "rotation", new float[] { 0f, 0f, 0f, 1f }),
                Scale = Floats(element, "scale", new float[] { 1f, 1f, 1f }),
                Mesh = Int(element, "mesh"),
                Skin = Int(element, "skin"),
            };

            if (element.TryGetProperty("children", out JsonElement children))
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    node.Children.Add(child.GetInt32());
                }
            }

            nodes.Add(node);
        }

        foreach (GlbNode node in nodes)
        {
            Nodes.Add(node);
        }

        JsonElement scene = doc.GetProperty("scenes")[doc.GetProperty("scene").GetInt32()];
        foreach (JsonElement root in scene.GetProperty("nodes").EnumerateArray())
        {
            SceneRoots.Add(root.GetInt32());
        }

        if (doc.TryGetProperty("meshes", out JsonElement meshes))
        {
            foreach (JsonElement element in meshes.EnumerateArray())
            {
                foreach (JsonElement prim in element.GetProperty("primitives").EnumerateArray())
                {
                    int mode = prim.TryGetProperty("mode", out JsonElement m) ? m.GetInt32() : 4;
                    if (mode != 4)
                    {
                        throw new InvalidDataException("Primitive mode " + mode.ToString(CultureInfo.InvariantCulture) +
                                                       " is not TRIANGLES; the runtime only draws triangle lists.");
                    }

                    JsonElement attributes = prim.GetProperty("attributes");
                    Primitives.Add(new GlbPrimitive
                    {
                        PositionAccessor = attributes.GetProperty("POSITION").GetInt32(),
                        NormalAccessor = attributes.TryGetProperty("NORMAL", out JsonElement nrm) ? nrm.GetInt32() : -1,
                        JointAccessor = attributes.TryGetProperty("JOINTS_0", out JsonElement jnt) ? jnt.GetInt32() : null,
                        WeightAccessor = attributes.TryGetProperty("WEIGHTS_0", out JsonElement wgt) ? wgt.GetInt32() : null,
                        IndexAccessor = prim.GetProperty("indices").GetInt32(),
                        Material = prim.TryGetProperty("material", out JsonElement mat) ? mat.GetInt32() : 0,
                    });
                }
            }
        }

        if (doc.TryGetProperty("materials", out JsonElement materialList))
        {
            foreach (JsonElement element in materialList.EnumerateArray())
            {
                JsonElement pbr = element.GetProperty("pbrMetallicRoughness");
                Materials.Add(new GlbMaterial
                {
                    Name = Str(element, "name"),
                    BaseColorFactor = Floats(pbr, "baseColorFactor", new float[] { 1f, 1f, 1f, 1f }),
                    RoughnessFactor = pbr.TryGetProperty("roughnessFactor", out JsonElement rough) ? rough.GetSingle() : 1f,
                    MetallicFactor = pbr.TryGetProperty("metallicFactor", out JsonElement metal) ? metal.GetSingle() : 0f,
                });
            }
        }

        // A skin is optional in glTF and absent from every static asset - a weapon is rigid
        // geometry with no skeleton. Reading it as required would make the reader
        // character-only, so an asset with no skin is parsed as having no bones rather
        // than rejected; the character converter then refuses the empty skeleton by name.
        int[] joints = Array.Empty<int>();
        JsonElement skin = default;
        if (doc.TryGetProperty("skins", out JsonElement skinList) && skinList.GetArrayLength() > 0)
        {
            skin = skinList[0];
            joints = new int[skin.GetProperty("joints").GetArrayLength()];
            int at = 0;
            foreach (JsonElement joint in skin.GetProperty("joints").EnumerateArray())
            {
                joints[at++] = joint.GetInt32();
            }
        }

        // glTF nodes have no parent field; derive it from the children lists.
        // Sockets resolve their attachment through this, so it has to be recorded on
        // the nodes themselves rather than kept in a local dictionary.
        for (int i = 0; i < nodes.Count; i++)
        {
            foreach (int child in nodes[i].Children)
            {
                nodes[child].Parent = i;
            }
        }

        Dictionary<int, int> parentOf = new();
        for (int i = 0; i < nodes.Count; i++)
        {
            foreach (int child in nodes[i].Children)
            {
                parentOf[child] = i;
            }
        }

        Dictionary<int, int> slotOf = new();
        for (int i = 0; i < joints.Length; i++)
        {
            slotOf[joints[i]] = i;
        }

        foreach (int joint in joints)
        {
            BoneNames.Add(nodes[joint].Name);
            JointNodes.Add(joint);
            BoneParents.Add(parentOf.TryGetValue(joint, out int parent) && slotOf.TryGetValue(parent, out int slot)
                ? slot
                : -1);
        }

        if (joints.Length > 0 && skin.TryGetProperty("inverseBindMatrices", out JsonElement ibm))
        {
            float[] flat = ReadFloats(ibm.GetInt32());
            for (int i = 0; i + 15 < flat.Length; i += 16)
            {
                // Copy the accessor straight through. Do NOT transpose.
                //
                // glTF stores these column-major, so flat[c * 4 + r] is element (r, c).
                // The runtime needs the row-vector form, whose element (r, c) is glTF's
                // (c, r) - and reading column-major already hands over exactly that, so
                // the indices line up without any swapping.
                //
                // Transposing on top of that is not a no-op: it moves the translation out
                // of the last row and into the last column, where a row-vector multiply
                // ignores it. Every bone then collapses onto the world origin. The failure
                // is invisible in the bind pose, because IBM * inverse(IBM) is identity
                // however wrong IBM is, so the mesh still looks correct until a clip moves
                // a bone.
                float[] matrix = new float[16];
                Array.Copy(flat, i, matrix, 0, 16);
                InverseBinds.Add(matrix);
            }
        }

        // The skinned mesh node: the one carrying both a mesh and this skin.
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Mesh.HasValue && nodes[i].Skin.HasValue)
            {
                MeshNode = i;
            }
        }

        CollectSockets(nodes, new HashSet<int>(joints));

        if (doc.TryGetProperty("animations", out JsonElement animations))
        {
            foreach (JsonElement element in animations.EnumerateArray())
            {
                GlbAnimation animation = new() { Name = Str(element, "name") };
                JsonElement samplers = element.GetProperty("samplers");
                JsonElement samplerList = samplers;

                int channelIndex = 0;
                foreach (JsonElement channel in element.GetProperty("channels").EnumerateArray())
                {
                    JsonElement sampler = samplerList[channel.GetProperty("sampler").GetInt32()];
                    int input = sampler.GetProperty("input").GetInt32();
                    int output = sampler.GetProperty("output").GetInt32();

                    int components = ComponentsOf(_accessors[output].GetProperty("type").GetString()!);

                    animation.Channels.Add(new GlbChannel
                    {
                        Node = channel.GetProperty("target").GetProperty("node").GetInt32(),
                        Path = channel.GetProperty("target").GetProperty("path").GetString() ?? string.Empty,
                        Times = ReadFloats(input),
                        Values = ReadFloats(output),
                        ComponentsPerKey = components,
                    });

                    channelIndex++;
                }

                Animations.Add(animation);
            }
        }
    }

    /// <summary>
    /// Finds attachment points in the node graph.
    ///
    /// A socket is a node that has a name, carries no mesh or skin, and is neither a
    /// joint nor the skinned mesh node itself. That deliberately includes a socket
    /// parented to another socket - MuzzlePoint hangs off WeaponSocket, and that chain
    /// is resolved at runtime, not flattened here, so a muzzle point keeps working if
    /// the weapon socket is moved later.
    ///
    /// The armature root and any scene-level helper (camera, light) is skipped: only
    /// nodes descending from a joint are attachment points, so anything else in the file
    /// is not part of the character.
    /// </summary>
    private void CollectSockets(List<GlbNode> nodes, HashSet<int> joints)
    {
        bool descendsFromJoint(int node)
        {
            int current = node;
            int guard = 0;

            while (current >= 0 && guard++ <= nodes.Count)
            {
                if (joints.Contains(current))
                {
                    return true;
                }

                current = nodes[current].Parent;
            }

            return false;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            GlbNode node = nodes[i];

            if (string.IsNullOrEmpty(node.Name) || node.Mesh.HasValue || node.Skin.HasValue)
            {
                continue;
            }

            if (joints.Contains(i) || i == MeshNode)
            {
                continue;
            }

            if (!descendsFromJoint(i))
            {
                continue;
            }

            Sockets.Add(new GlbSocket
            {
                Name = node.Name,
                Node = i,
                ParentNode = node.Parent,
                Translation = node.Translation,
                Rotation = node.Rotation,
                Scale = node.Scale,
            });
        }
    }

    /// <summary>Reads an accessor as floats, honouring byteStride for interleaved views.</summary>
    public float[] ReadFloats(int accessorIndex)
    {
        JsonElement accessor = _accessors[accessorIndex];
        GlbComponent component = (GlbComponent)accessor.GetProperty("componentType").GetInt32();
        int count = accessor.GetProperty("count").GetInt32();
        int components = ComponentsOf(accessor.GetProperty("type").GetString()!);

        int elementSize = component switch
        {
            GlbComponent.UnsignedByte or GlbComponent.Byte => 1,
            GlbComponent.UnsignedShort or GlbComponent.Short => 2,
            GlbComponent.Float or GlbComponent.UnsignedInt => 4,
            _ => throw new InvalidDataException("Unsupported component type."),
        };

        JsonElement view = _bufferViews[accessor.GetProperty("bufferView").GetInt32()];
        int viewStart = view.TryGetProperty("byteOffset", out JsonElement viewOffset) ? viewOffset.GetInt32() : 0;
        int stride = view.TryGetProperty("byteStride", out JsonElement s) ? s.GetInt32() : elementSize * components;
        int start = viewStart + (accessor.TryGetProperty("byteOffset", out JsonElement aOffset) ? aOffset.GetInt32() : 0);

        float[] result = new float[count * components];
        for (int i = 0; i < count; i++)
        {
            int at = start + (i * stride);
            for (int c = 0; c < components; c++)
            {
                int offset = at + (c * elementSize);
                result[(i * components) + c] = component switch
                {
                    GlbComponent.Float => BitConverter.ToSingle(_binary, offset),
                    GlbComponent.UnsignedByte => _binary[offset] / 255f,
                    GlbComponent.UnsignedShort => BitConverter.ToUInt16(_binary, offset),
                    GlbComponent.UnsignedInt => BitConverter.ToUInt32(_binary, offset),
                    GlbComponent.Short => BitConverter.ToInt16(_binary, offset),
                    GlbComponent.Byte => _binary[offset],
                    _ => throw new InvalidDataException("Unsupported component type."),
                };
            }
        }

        return result;
    }

    /// <summary>Reads an index accessor as ints, whatever width it was stored at.</summary>
    public int[] ReadIndices(int accessorIndex)
    {
        JsonElement accessor = _accessors[accessorIndex];
        GlbComponent component = (GlbComponent)accessor.GetProperty("componentType").GetInt32();
        int count = accessor.GetProperty("count").GetInt32();

        JsonElement view = _bufferViews[accessor.GetProperty("bufferView").GetInt32()];
        int viewStart = view.TryGetProperty("byteOffset", out JsonElement viewOffset) ? viewOffset.GetInt32() : 0;
        int elementSize = component == GlbComponent.UnsignedShort ? 2 : 4;
        int stride = view.TryGetProperty("byteStride", out JsonElement s) ? s.GetInt32() : elementSize;
        int start = viewStart + (accessor.TryGetProperty("byteOffset", out JsonElement aOffset) ? aOffset.GetInt32() : 0);

        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int offset = start + (i * stride);
            result[i] = elementSize == 2 ? BitConverter.ToUInt16(_binary, offset) : (int)BitConverter.ToUInt32(_binary, offset);
        }

        return result;
    }

    private static int ComponentsOf(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        "MAT4" => 16,
        _ => throw new InvalidDataException("Unsupported accessor type " + type + "."),
    };

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : null;

    private static float[] Floats(JsonElement element, string name, float[] fallback)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return fallback;
        }

        float[] result = new float[value.GetArrayLength()];
        int i = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            result[i++] = item.GetSingle();
        }

        return result;
    }
}

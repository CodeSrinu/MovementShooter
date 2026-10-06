using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;

namespace MovementShooter.Character;

/// <summary>One material's flat shading parameters, as authored in Blender.</summary>
/// <param name="Name">Authored name, kept for diagnostics.</param>
/// <param name="Color">Linear base colour. The renderer shades in linear space, so this is not gamma converted.</param>
/// <param name="Roughness">Authored roughness, retained for parity with the source even though the
/// current lit material does not vary specular per surface.</param>
/// <param name="Metallic">Authored metalness, retained for the same reason.</param>
public readonly record struct CharacterMaterial(string Name, Vector3 Color, float Roughness, float Metallic);

/// <summary>A contiguous index range drawn with one material.</summary>
/// <param name="FirstIndex">Offset into the shared index buffer.</param>
/// <param name="IndexCount">Number of indices, always a multiple of three.</param>
/// <param name="MaterialIndex">Index into <see cref="CharacterAsset.Materials"/>.</param>
public readonly record struct CharacterSubmesh(int FirstIndex, int IndexCount, int MaterialIndex);

/// <summary>One bone in the skeleton.</summary>
/// <param name="Name">Authored name.</param>
/// <param name="ParentIndex">Parent bone, or -1 for the single root.</param>
/// <param name="InverseBind">Inverse bind matrix, row-vector convention.</param>
public readonly record struct CharacterBone(string Name, int ParentIndex, Matrix InverseBind);

/// <summary>
/// An attachment point, such as a weapon socket or a muzzle point.
///
/// These are authored as Blender Empties parented to bones, not as bones. They carry no
/// skin weights, are never part of the skeleton, and must not be looked up through
/// <see cref="CharacterAsset.IndexOfBone"/> - the distinction is preserved so a weapon can
/// be attached without the weapon's transform being confused with the character's.
/// </summary>
/// <param name="Name">Authored name.</param>
/// <param name="ParentBone">Bone this socket hangs off, or -1 when parented to another socket.</param>
/// <param name="ParentSocket">Socket this socket hangs off, or -1.</param>
/// <param name="Local">Offset in the parent's space, row-vector convention.</param>
public readonly record struct CharacterSocket(string Name, int ParentBone, int ParentSocket, Matrix Local);

/// <summary>A vertex in bind pose, with the four bones that move it.</summary>
public readonly record struct CharacterVertex(
    Vector3 BindPosition,
    Vector3 BindNormal,
    int Joint0, int Joint1, int Joint2, int Joint3,
    float Weight0, float Weight1, float Weight2, float Weight3,
    Color Color);

/// <summary>
/// One animation clip, resampled onto a uniform time grid at conversion time.
///
/// Times are in seconds. Translation and rotation are stored per frame per bone,
/// indexed as <c>(frame * BoneCount + bone)</c> - bone-major within a frame so a
/// whole skeleton can be read with one stride.
/// </summary>
public sealed class CharacterClip
{
    internal CharacterClip(string name, float duration, float[] times, float[] translation, float[] rotation, int boneCount)
    {
        Name = name;
        Duration = duration;
        Times = times;
        Translation = translation;
        Rotation = rotation;
        BoneCount = boneCount;
    }

    public string Name { get; }

    /// <summary>Length of the clip in seconds.</summary>
    public float Duration { get; }

    /// <summary>Sample times in seconds, ascending, starting at zero.</summary>
    public float[] Times { get; }

    internal float[] Translation { get; }

    internal float[] Rotation { get; }

    internal int BoneCount { get; }

    public int FrameCount => Times.Length;
}

/// <summary>
/// The whole character in memory: bind-pose mesh, skeleton, materials and clips.
///
/// Immutable once loaded. Built by <see cref="Load"/> from the blob emitted by the
/// build-time GLB converter; nothing here reads a file at runtime, so the game has
/// no asset path to get wrong.
/// </summary>
public sealed class CharacterAsset
{
    /// <summary>Manifest name of the embedded blob. Set explicitly in the csproj so it cannot drift with folders.</summary>
    public const string ResourceName = "KINETIC_Character.bin";

    private const string Magic = "KINCHAR2";
    private const int FormatVersion = 2;

    private CharacterAsset(
        CharacterBone[] bones,
        CharacterSocket[] sockets,
        CharacterVertex[] vertices,
        int[] indices,
        CharacterSubmesh[] submeshes,
        CharacterMaterial[] materials,
        CharacterClip[] clips,
        float minY,
        float maxY)
    {
        Bones = bones;
        Sockets = sockets;
        Vertices = vertices;
        Indices = indices;
        Submeshes = submeshes;
        Materials = materials;
        Clips = clips;
        MinY = minY;
        MaxY = maxY;
    }

    public CharacterBone[] Bones { get; }

    /// <summary>
    /// Attachment points, empty when the character was exported without them. These are
    /// not bones and are deliberately kept out of <see cref="Bones"/>.
    /// </summary>
    public CharacterSocket[] Sockets { get; }

    public CharacterVertex[] Vertices { get; }

    public int[] Indices { get; }

    public CharacterSubmesh[] Submeshes { get; }

    public CharacterMaterial[] Materials { get; }

    public CharacterClip[] Clips { get; }

    /// <summary>Lowest bind-pose Y, which is the soles of the feet.</summary>
    public float MinY { get; }

    /// <summary>Highest bind-pose Y, the top of the head.</summary>
    public float MaxY { get; }

    /// <summary>Authored height in metres.</summary>
    public float Height => MaxY - MinY;

    public int BoneCount => Bones.Length;

    public int VertexCount => Vertices.Length;

    public int TriangleCount => Indices.Length / 3;

    /// <summary>Index of the bone named <paramref name="name"/>, or -1.</summary>
    public int IndexOfBone(string name)
    {
        for (int i = 0; i < Bones.Length; i++)
        {
            if (Bones[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Index of the socket named <paramref name="name"/>, or -1.
    /// Sockets are separate from bones, so this never returns a bone index.
    /// </summary>
    public int IndexOfSocket(string name)
    {
        for (int i = 0; i < Sockets.Length; i++)
        {
            if (Sockets[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Index of the clip named <paramref name="name"/>, or -1.</summary>
    public int IndexOfClip(string name)
    {
        for (int i = 0; i < Clips.Length; i++)
        {
            if (Clips[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Loads the embedded character blob.
    /// </summary>
    /// <exception cref="InvalidDataException">The blob is missing, truncated, or not the expected format.</exception>
    public static CharacterAsset Load()
    {
        return Load(ReadEmbedded(ResourceName));
    }

    /// <summary>Loads a character blob from a stream. Exposed so the headless checks can build one from bytes.</summary>
    public static CharacterAsset Load(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != Magic)
        {
            throw new InvalidDataException(
                "Not a KINETIC character asset: expected magic " + Magic + ", got '" + magic + "'.");
        }

        int version = reader.ReadInt32();
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                "Character asset format version " + version + " is not the supported version " +
                FormatVersion + ".");
        }

        int vertexCount = reader.ReadInt32();
        int indexCount = reader.ReadInt32();
        int boneCount = reader.ReadInt32();
        int submeshCount = reader.ReadInt32();
        int materialCount = reader.ReadInt32();
        int clipCount = reader.ReadInt32();
        int socketCount = reader.ReadInt32();
        float minY = reader.ReadSingle();
        float maxY = reader.ReadSingle();

        if (vertexCount <= 0 || indexCount <= 0 || boneCount <= 0)
        {
            throw new InvalidDataException("Character asset has empty geometry or skeleton.");
        }

        CharacterBone[] bones = new CharacterBone[boneCount];
        for (int i = 0; i < boneCount; i++)
        {
            string name = ReadString(reader);
            int parent = reader.ReadInt32();

            Matrix inverseBind = new(
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            bones[i] = new CharacterBone(name, parent, inverseBind);
        }

        // Sockets follow the bones. Their parent fields index into the bone list or this
        // one, so both have to be read before any of them can be validated.
        CharacterSocket[] sockets = new CharacterSocket[socketCount];
        for (int i = 0; i < socketCount; i++)
        {
            string name = ReadString(reader);
            int parentBone = reader.ReadInt32();
            int parentSocket = reader.ReadInt32();

            Matrix local = new(
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            sockets[i] = new CharacterSocket(name, parentBone, parentSocket, local);
        }

        CharacterSubmesh[] submeshes = new CharacterSubmesh[submeshCount];
        for (int i = 0; i < submeshCount; i++)
        {
            submeshes[i] = new CharacterSubmesh(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        CharacterMaterial[] materials = new CharacterMaterial[materialCount];
        for (int i = 0; i < materialCount; i++)
        {
            string name = ReadString(reader);
            float r = reader.ReadSingle();
            float g = reader.ReadSingle();
            float b = reader.ReadSingle();
            float roughness = reader.ReadSingle();
            float metallic = reader.ReadSingle();
            materials[i] = new CharacterMaterial(name, new Vector3(r, g, b), roughness, metallic);
        }

        CharacterVertex[] vertices = new CharacterVertex[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 position = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Vector3 normal = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            int j0 = reader.ReadInt32();
            int j1 = reader.ReadInt32();
            int j2 = reader.ReadInt32();
            int j3 = reader.ReadInt32();
            float w0 = reader.ReadSingle();
            float w1 = reader.ReadSingle();
            float w2 = reader.ReadSingle();
            float w3 = reader.ReadSingle();
            byte r = reader.ReadByte();
            byte g = reader.ReadByte();
            byte b = reader.ReadByte();
            byte a = reader.ReadByte();

            vertices[i] = new CharacterVertex(position, normal, j0, j1, j2, j3, w0, w1, w2, w3, new Color(r, g, b, a));
        }

        // 16-bit indices below 65536 vertices, 32-bit above; the writer decides, so
        // the reader has to agree without being told.
        bool wide = vertexCount > ushort.MaxValue;
        int[] indices = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
        {
            indices[i] = wide ? reader.ReadInt32() : reader.ReadUInt16();
        }

        CharacterClip[] clips = new CharacterClip[clipCount];
        for (int i = 0; i < clipCount; i++)
        {
            string name = ReadString(reader);
            float duration = reader.ReadSingle();
            int frameCount = reader.ReadInt32();

            float[] times = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                times[f] = reader.ReadSingle();
            }

            float[] translation = new float[frameCount * boneCount * 3];
            for (int t = 0; t < translation.Length; t++)
            {
                translation[t] = reader.ReadSingle();
            }

            float[] rotation = new float[frameCount * boneCount * 4];
            for (int r = 0; r < rotation.Length; r++)
            {
                rotation[r] = reader.ReadSingle();
            }

            clips[i] = new CharacterClip(name, duration, times, translation, rotation, boneCount);
        }

        return new CharacterAsset(bones, sockets, vertices, indices, submeshes, materials, clips, minY, maxY);
    }

    private static Stream ReadEmbedded(string resourceName)
    {
        Assembly assembly = typeof(CharacterAsset).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            List<string> available = new();
            foreach (string name in assembly.GetManifestResourceNames())
            {
                available.Add(name);
            }

            throw new InvalidDataException(
                "Embedded character asset '" + resourceName + "' is missing. Present: " +
                (available.Count == 0 ? "(none)" : string.Join(", ", available)) +
                ". Regenerate with the KineticAssetTool and check the csproj EmbeddedResource entry.");
        }

        return stream;
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > 256)
        {
            throw new InvalidDataException("Character asset contains an implausible string length " + length + ".");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
}

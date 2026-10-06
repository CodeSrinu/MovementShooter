using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;

namespace MovementShooter.Character;

/// <summary>One material's flat shading parameters, as authored for the weapon.</summary>
public readonly record struct WeaponMaterial(string Name, Vector3 Color, float Roughness, float Metallic);

/// <summary>A contiguous index range drawn with one material.</summary>
public readonly record struct WeaponSubmesh(int FirstIndex, int IndexCount, int MaterialIndex);

/// <summary>
/// A rigid weapon in weapon space.
///
/// Deliberately separate from <see cref="CharacterAsset"/>: a weapon has no skeleton, no
/// skin weights and no animation. Sharing the character's blob would mean a reader full
/// of empty fields branching on which kind it loaded, so each format matches its asset.
///
/// The weapon's origin is its grip and it points along its own +Z, matching the
/// character's convention. That is what lets the runtime attach it by handing the socket's
/// world matrix straight to the draw call - no corrective rotation, no fudge factor.
/// </summary>
public sealed class WeaponAsset
{
    /// <summary>Manifest name of the embedded blob. Set explicitly in the csproj.</summary>
    public const string ResourceName = "KINETIC_RocketLauncher.bin";

    private const string Magic = "KINWEAP1";
    private const int FormatVersion = 1;

    private WeaponAsset(
        string name,
        Vector3[] positions,
        Vector3[] normals,
        Color[] colors,
        int[] indices,
        WeaponSubmesh[] submeshes,
        WeaponMaterial[] materials,
        Vector3 muzzle,
        bool hasMuzzle,
        float minY,
        float maxY)
    {
        Name = name;
        Positions = positions;
        Normals = normals;
        Colors = colors;
        Indices = indices;
        Submeshes = submeshes;
        Materials = materials;
        Muzzle = muzzle;
        HasAuthoredMuzzle = hasMuzzle;
        MinY = minY;
        MaxY = maxY;
    }

    public string Name { get; }

    /// <summary>Positions in weapon space, the origin at the grip.</summary>
    public Vector3[] Positions { get; }

    public Vector3[] Normals { get; }

    public Color[] Colors { get; }

    public int[] Indices { get; }

    public WeaponSubmesh[] Submeshes { get; }

    public WeaponMaterial[] Materials { get; }

    /// <summary>
    /// The authored muzzle position in weapon space.
    ///
    /// Authored, not derived: a launcher whose muzzle bell flares wider than its bore
    /// would report the bell's outer edge if this were computed from the mesh bounds,
    /// putting every rocket in flight slightly wide of where the barrel points.
    /// </summary>
    public Vector3 Muzzle { get; }

    public bool HasAuthoredMuzzle { get; }

    public float MinY { get; }

    public float MaxY { get; }

    public int VertexCount => Positions.Length;

    public int TriangleCount => Indices.Length / 3;

    /// <summary>Furthest the mesh reaches along +Z, which is how far the weapon points.</summary>
    public float ForwardExtent { get; private set; }

    /// <summary>Nearest the mesh reaches along +Z, behind the grip. Negative for a grip held mid-weapon.</summary>
    public float BackwardExtent { get; private set; }

    /// <summary>Loads the embedded weapon blob.</summary>
    public static WeaponAsset Load() => Load(ReadEmbedded(ResourceName));

    /// <summary>Loads a weapon blob from a stream, so the headless checks can build one from bytes.</summary>
    public static WeaponAsset Load(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != Magic)
        {
            throw new InvalidDataException(
                "Not a KINETIC weapon asset: expected magic " + Magic + ", got '" + magic + "'.");
        }

        int version = reader.ReadInt32();
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                "Weapon asset format version " + version + " is not the supported version " +
                FormatVersion + ".");
        }

        int vertexCount = reader.ReadInt32();
        int indexCount = reader.ReadInt32();
        int submeshCount = reader.ReadInt32();
        int materialCount = reader.ReadInt32();
        bool hasMuzzle = reader.ReadInt32() != 0;
        string name = ReadString(reader);

        Vector3 muzzle = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        float minY = reader.ReadSingle();
        float maxY = reader.ReadSingle();

        if (vertexCount <= 0 || indexCount <= 0)
        {
            throw new InvalidDataException("Weapon asset has no geometry.");
        }

        WeaponSubmesh[] submeshes = new WeaponSubmesh[submeshCount];
        for (int i = 0; i < submeshCount; i++)
        {
            submeshes[i] = new WeaponSubmesh(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        WeaponMaterial[] materials = new WeaponMaterial[materialCount];
        for (int i = 0; i < materialCount; i++)
        {
            string materialName = ReadString(reader);
            float r = reader.ReadSingle();
            float g = reader.ReadSingle();
            float b = reader.ReadSingle();
            float roughness = reader.ReadSingle();
            float metallic = reader.ReadSingle();
            materials[i] = new WeaponMaterial(materialName, new Vector3(r, g, b), roughness, metallic);
        }

        Vector3[] positions = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        float forward = float.MinValue;
        float backward = float.MaxValue;

        for (int i = 0; i < vertexCount; i++)
        {
            positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            byte r = reader.ReadByte();
            byte g = reader.ReadByte();
            byte b = reader.ReadByte();
            byte a = reader.ReadByte();
            colors[i] = new Color(r, g, b, a);

            forward = MathF.Max(forward, positions[i].Z);
            backward = MathF.Min(backward, positions[i].Z);
        }

        // 16-bit indices below 65536 vertices, 32-bit above; the writer decides, so the
        // reader has to agree without being told.
        bool wide = vertexCount > ushort.MaxValue;
        int[] indices = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
        {
            indices[i] = wide ? reader.ReadInt32() : reader.ReadUInt16();
        }

        WeaponAsset asset = new(name, positions, normals, colors, indices, submeshes, materials,
            muzzle, hasMuzzle, minY, maxY);
        asset.ForwardExtent = forward;
        asset.BackwardExtent = backward;
        return asset;
    }

    private static Stream ReadEmbedded(string resourceName)
    {
        Assembly assembly = typeof(WeaponAsset).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidDataException(
                "Embedded weapon asset '" + resourceName + "' is missing. Regenerate it with " +
                "tools\\KineticAssetTool using --weapon, and check the csproj EmbeddedResource entry.");
        }

        return stream;
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > 256)
        {
            throw new InvalidDataException("Weapon asset contains an implausible string length " + length + ".");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
}
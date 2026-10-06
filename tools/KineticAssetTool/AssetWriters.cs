using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace KineticAssetTool;

/// <summary>One material, as the runtime needs it.</summary>
internal readonly record struct ConvertedMaterial(string Name, float R, float G, float B, float Roughness, float Metallic);

/// <summary>One contiguous index range sharing a material.</summary>
internal readonly record struct ConvertedSubmesh(int FirstIndex, int IndexCount, int MaterialIndex);

/// <summary>One skinned vertex in bind pose.</summary>
internal struct ConvertedVertex
{
    public float Px, Py, Pz;
    public float Nx, Ny, Nz;
    public int J0, J1, J2, J3;
    public float W0, W1, W2, W3;
    public byte R, G, B, A;
}

/// <summary>One animation clip, resampled onto a uniform time grid.</summary>
internal sealed class ConvertedClip
{
    public string Name { get; init; } = string.Empty;
    public float[] Times { get; init; } = Array.Empty<float>();
    public float[] Translation { get; init; } = Array.Empty<float>();
    public float[] Rotation { get; init; } = Array.Empty<float>();
    public float Duration { get; set; }
}

/// <summary>
/// An attachment point, resolved against the skeleton.
///
/// The authored sockets are Blender Empties parented to bones, not bones, so they
/// cannot be folded into the bone list without losing the distinction. Each socket
/// names the bone it hangs off (or another socket, via <see cref="ParentSocket"/>) plus
/// its offset in that parent's space.
/// </summary>
internal sealed class ConvertedSocket
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Bone index this socket is attached to, or -1 when parented to another socket.</summary>
    public int ParentBone { get; init; } = -1;

    /// <summary>Socket index this socket is attached to, or -1.</summary>
    public int ParentSocket { get; init; } = -1;

    /// <summary>Local transform in the parent's space, row-vector convention.</summary>
    public float[] Local { get; init; } = new float[16];
}

/// <summary>Everything the runtime needs, in the layout <see cref="CharacterAssetWriter"/> emits.</summary>
internal sealed class ConvertedAsset
{
    public List<string> BoneNames { get; } = new();
    public List<int> BoneParents { get; } = new();
    public List<float[]> InverseBinds { get; } = new();
    public List<ConvertedSocket> Sockets { get; } = new();
    public List<ConvertedVertex> Vertices { get; } = new();
    public List<int> Indices { get; } = new();
    public List<ConvertedSubmesh> Submeshes { get; } = new();
    public List<ConvertedMaterial> Materials { get; } = new();
    public List<ConvertedClip> Clips { get; } = new();
    public float MinY { get; set; }
    public float MaxY { get; set; }
}

/// <summary>Everything the runtime needs for a rigid weapon.</summary>
internal sealed class ConvertedWeapon
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Authored muzzle position in weapon space. Not a bound: the mesh's extent is not
    /// the same thing as where a rocket leaves the barrel, and a launcher whose bell is
    /// wider than its bore would report the wrong point from its bounds.
    /// </summary>
    public float[] Muzzle { get; set; } = { 0f, 0f, 0f };

    public bool HasMuzzle { get; set; }

    public List<ConvertedVertex> Vertices { get; } = new();
    public List<int> Indices { get; } = new();
    public List<ConvertedSubmesh> Submeshes { get; } = new();
    public List<ConvertedMaterial> Materials { get; } = new();
    public float MinY { get; set; }
    public float MaxY { get; set; }
}

/// <summary>
/// Writes the runtime character asset.
///
/// The format is deliberately dumb: a fixed header, then flat arrays. The
/// runtime needs one mesh, four flat colours, a 17-bone palette and eight clips,
/// and nothing here is streamed or partially read, so a compact whole-file blob
/// beats a format that supports features this game does not have.
///
/// Scale is not stored. The converter refuses to emit an asset whose scale
/// channels are not identity rather than quietly baking them into translation,
/// because a non-uniform scale would need its own inverse-transpose path in the
/// skinning and silently dropping it is how characters end up with collapsed
/// limbs.
/// </summary>
internal static class CharacterAssetWriter
{
    private const string Magic = "KINCHAR2";

    public static void Write(string path, ConvertedAsset asset)
    {
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(2);                                     // format version; must match CharacterAsset.FormatVersion
        writer.Write(asset.Vertices.Count);
        writer.Write(asset.Indices.Count);
        writer.Write(asset.BoneNames.Count);
        writer.Write(asset.Submeshes.Count);
        writer.Write(asset.Materials.Count);
        writer.Write(asset.Clips.Count);
        writer.Write(asset.Sockets.Count);
        writer.Write(asset.MinY);
        writer.Write(asset.MaxY);

        for (int i = 0; i < asset.BoneNames.Count; i++)
        {
            WriteString(writer, asset.BoneNames[i]);
            writer.Write(asset.BoneParents[i]);
            foreach (float value in asset.InverseBinds[i])
            {
                writer.Write(value);
            }
        }

        // Sockets come straight after the bones: the parent fields are indices into
        // those two lists, so both must be written before anything resolves them.
        foreach (ConvertedSocket socket in asset.Sockets)
        {
            WriteString(writer, socket.Name);
            writer.Write(socket.ParentBone);
            writer.Write(socket.ParentSocket);
            foreach (float value in socket.Local)
            {
                writer.Write(value);
            }
        }

        foreach (ConvertedSubmesh submesh in asset.Submeshes)
        {
            writer.Write(submesh.FirstIndex);
            writer.Write(submesh.IndexCount);
            writer.Write(submesh.MaterialIndex);
        }

        foreach (ConvertedMaterial material in asset.Materials)
        {
            WriteString(writer, material.Name);
            writer.Write(material.R);
            writer.Write(material.G);
            writer.Write(material.B);
            writer.Write(material.Roughness);
            writer.Write(material.Metallic);
        }

        foreach (ConvertedVertex vertex in asset.Vertices)
        {
            writer.Write(vertex.Px); writer.Write(vertex.Py); writer.Write(vertex.Pz);
            writer.Write(vertex.Nx); writer.Write(vertex.Ny); writer.Write(vertex.Nz);
            writer.Write(vertex.J0); writer.Write(vertex.J1);
            writer.Write(vertex.J2); writer.Write(vertex.J3);
            writer.Write(vertex.W0); writer.Write(vertex.W1);
            writer.Write(vertex.W2); writer.Write(vertex.W3);
            writer.Write(vertex.R); writer.Write(vertex.G);
            writer.Write(vertex.B); writer.Write(vertex.A);
        }

        bool wide = asset.Vertices.Count > ushort.MaxValue;
        foreach (int index in asset.Indices)
        {
            if (wide)
            {
                writer.Write(index);
            }
            else
            {
                writer.Write((ushort)index);
            }
        }

        foreach (ConvertedClip clip in asset.Clips)
        {
            WriteString(writer, clip.Name);
            writer.Write(clip.Duration);
            writer.Write(clip.Times.Length);
            foreach (float time in clip.Times)
            {
                writer.Write(time);
            }

            foreach (float value in clip.Translation)
            {
                writer.Write(value);
            }

            foreach (float value in clip.Rotation)
            {
                writer.Write(value);
            }
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        // Length-prefixed UTF-8: fixed-width would waste space on the eight clip
        // names and the bone names, which are all under twenty characters.
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static string Describe(ConvertedAsset asset)
    {
        StringBuilder text = new();
        void Line(string label, object value) =>
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-14} {1}", label, value));
        void Detail(string value) => text.AppendLine(value);

        Line("vertices", asset.Vertices.Count);
        Line("triangles", asset.Indices.Count / 3);
        Line("indices", asset.Indices.Count);
        Line("bones", asset.BoneNames.Count);
        Line("submeshes", asset.Submeshes.Count);
        Line("materials", asset.Materials.Count);
        Line("clips", asset.Clips.Count);
        Line("sockets", asset.Sockets.Count);
        foreach (ConvertedSocket socket in asset.Sockets)
        {
            string parent = socket.ParentSocket >= 0
                ? "socket " + asset.Sockets[socket.ParentSocket].Name + " on bone " +
                  (socket.ParentBone >= 0 ? asset.BoneNames[socket.ParentBone] : "(none)")
                : "bone " + (socket.ParentBone >= 0 ? asset.BoneNames[socket.ParentBone] : "(none)");

            Vector3 translation = new(socket.Local[12], socket.Local[13], socket.Local[14]);
            Detail(string.Format(CultureInfo.InvariantCulture,
                "  {0,-20} -> {1,-34} offset {2}", socket.Name, parent, Describe(translation)));
        }

        Line("height (m)", string.Format(CultureInfo.InvariantCulture, "{0:0.000} .. {1:0.000}", asset.MinY, asset.MaxY));
        return text.ToString();
    }

    private static string Describe(Vector3 translation) =>
        string.Format(CultureInfo.InvariantCulture,
            "({0,7:0.000}, {1,7:0.000}, {2,7:0.000})", translation.X, translation.Y, translation.Z);
}

/// <summary>
/// Writes the runtime weapon asset.
///
/// Deliberately a separate format from the character's rather than a variant of it. A
/// weapon has no skeleton, no skin weights and no animation, so sharing the
/// character's blob would mean a reader with seven empty-or-zero fields and branches on
/// which kind it loaded. Two small formats, each matching its asset, is less code to get
/// wrong.
///
/// Vertices are the same layout the character uses minus the four bone indices and
/// weights, and plus a flat vertex colour so the weapon uses the same lit material.
/// </summary>
internal static class WeaponAssetWriter
{
    private const string Magic = "KINWEAP1";

    public static void Write(string path, ConvertedWeapon weapon)
    {
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(1);                                     // format version
        writer.Write(weapon.Vertices.Count);
        writer.Write(weapon.Indices.Count);
        writer.Write(weapon.Submeshes.Count);
        writer.Write(weapon.Materials.Count);
        // Written as an int, not a bool: BinaryWriter.Write(bool) emits a single byte,
        // which the reader would then consume four bytes for and desynchronise the whole
        // rest of the blob. A flag as a byte is a format trap.
        writer.Write(weapon.HasMuzzle ? 1 : 0);
        WriteString(writer, weapon.Name);

        foreach (float value in weapon.Muzzle)
        {
            writer.Write(value);
        }

        writer.Write(weapon.MinY);
        writer.Write(weapon.MaxY);

        foreach (ConvertedSubmesh submesh in weapon.Submeshes)
        {
            writer.Write(submesh.FirstIndex);
            writer.Write(submesh.IndexCount);
            writer.Write(submesh.MaterialIndex);
        }

        foreach (ConvertedMaterial material in weapon.Materials)
        {
            WriteString(writer, material.Name);
            writer.Write(material.R);
            writer.Write(material.G);
            writer.Write(material.B);
            writer.Write(material.Roughness);
            writer.Write(material.Metallic);
        }

        foreach (ConvertedVertex vertex in weapon.Vertices)
        {
            writer.Write(vertex.Px); writer.Write(vertex.Py); writer.Write(vertex.Pz);
            writer.Write(vertex.Nx); writer.Write(vertex.Ny); writer.Write(vertex.Nz);
            writer.Write(vertex.R); writer.Write(vertex.G);
            writer.Write(vertex.B); writer.Write(vertex.A);
        }

        bool wide = weapon.Vertices.Count > ushort.MaxValue;
        foreach (int index in weapon.Indices)
        {
            if (wide)
            {
                writer.Write(index);
            }
            else
            {
                writer.Write((ushort)index);
            }
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    public static string Describe(ConvertedWeapon weapon)
    {
        StringBuilder text = new();
        void Line(string label, object value) =>
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-14} {1}", label, value));

        Line("name", weapon.Name);
        Line("vertices", weapon.Vertices.Count);
        Line("triangles", weapon.Indices.Count / 3);
        Line("indices", weapon.Indices.Count);
        Line("submeshes", weapon.Submeshes.Count);
        Line("materials", weapon.Materials.Count);
        Line("muzzle", weapon.HasMuzzle
            ? Describe(new Vector3(weapon.Muzzle[0], weapon.Muzzle[1], weapon.Muzzle[2]))
            : "(not authored)");
        Line("height (m)", string.Format(CultureInfo.InvariantCulture,
            "{0:0.000} .. {1:0.000}", weapon.MinY, weapon.MaxY));
        return text.ToString();
    }

    private static string Describe(Vector3 v) =>
        string.Format(CultureInfo.InvariantCulture,
            "({0,7:0.000}, {1,7:0.000}, {2,7:0.000})", v.X, v.Y, v.Z);
}
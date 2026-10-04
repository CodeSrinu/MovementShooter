using System;
using System.IO;
using System.IO.Compression;

namespace MovementShooter.Core;

/// <summary>
/// Minimal 8-bit RGBA PNG encoder built on BCL primitives only (no ImageSharp / System.Drawing).
/// It exists so automated screenshots can be written for visual verification.
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <param name="path">Destination file path. Intermediate folders are created.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="rgba">Row-major RGBA bytes, 4 bytes per pixel, top row first.</param>
    public static void Save(string path, int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        int expected = width * height * 4;
        if (rgba.Length < expected)
        {
            throw new ArgumentException($"Expected {expected} bytes of pixel data, got {rgba.Length}.", nameof(rgba));
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream output = File.Create(path);
        output.Write(Signature, 0, Signature.Length);

        // IHDR: 8-bit RGBA, no interlacing.
        byte[] header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: truecolour with alpha
        header[10] = 0; // compression: deflate
        header[11] = 0; // filter: adaptive
        header[12] = 0; // interlace: none
        WriteChunk(output, "IHDR", header);

        byte[] filtered = BuildFilteredScanlines(rgba, width, height);
        byte[] compressed = ZlibCompress(filtered);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", Array.Empty<byte>());
    }

    private static byte[] BuildFilteredScanlines(byte[] rgba, int width, int height)
    {
        int stride = width * 4;
        byte[] result = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int destination = y * (stride + 1);
            result[destination] = 0; // filter type "None"
            Buffer.BlockCopy(rgba, y * stride, result, destination + 1, stride);
        }

        return result;
    }

    private static byte[] ZlibCompress(byte[] raw)
    {
        using MemoryStream buffer = new();
        buffer.WriteByte(0x78); // zlib header: deflate, 32K window
        buffer.WriteByte(0x01); // no preset dictionary, fastest compression level

        using (DeflateStream deflate = new(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw, 0, raw.Length);
        }

        byte[] data = buffer.GetBuffer();
        WriteBigEndian(data, (int)buffer.Length - 4, Adler32(raw));

        byte[] result = new byte[buffer.Length];
        buffer.Position = 0;
        buffer.ReadExactly(result, 0, result.Length);
        return result;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] typeBytes = { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        stream.Write(length, 0, 4);
        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);

        uint crc = Crc32(typeBytes, data);
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        stream.Write(crcBytes, 0, 4);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Adler32(byte[] data)
    {
        const uint Modulus = 65521;
        uint a = 1;
        uint b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % Modulus;
            b = (b + a) % Modulus;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(byte[] first, byte[] second)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in first)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (byte value in second)
        {
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint entry = i;
            for (int bit = 0; bit < 8; bit++)
            {
                entry = (entry & 1) != 0 ? 0xEDB88320u ^ (entry >> 1) : entry >> 1;
            }

            table[i] = entry;
        }

        return table;
    }
}
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.UI;

/// <summary>
/// A tiny fixed-width bitmap font built in code, because the project deliberately has no content pipeline
/// (no fonts can be loaded from disk). Each glyph is 5 x 7 pixels.
/// </summary>
/// <remarks>
/// Glyph rows are authored as art so they stay editable; the texture atlas is generated on first use.
/// Lower case input is folded to upper case, which is all the debug display needs.
/// </remarks>
public sealed class BitmapFont : IDisposable
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    private const int ColumnCount = 16;

    /// <summary>
    /// Each entry is the character followed by seven rows of five cells ('#' is ink). Whitespace is
    /// ignored, so rows may be spaced out for readability. Only the characters the development display
    /// needs are here; lower case is folded to upper case at draw time.
    /// </summary>
    private static readonly string[] Glyphs =
    {
        " ..... ..... ..... ..... ..... ..... .....",
        "A .###. #...# #...# ##### #...# #...# #...#",
        "B ####. #...# #...# ####. #...# #...# ####.",
        "C .###. #...# #.... #.... #.... #...# .###.",
        "D ####. #...# #...# #...# #...# #...# ####.",
        "E ##### #.... #.... ####. #.... #.... #####",
        "F ##### #.... #.... ####. #.... #.... #....",
        "G .###. #...# #.... #.### #...# #...# .###.",
        "H #...# #...# #...# ##### #...# #...# #...#",
        "I ##### ..#.. ..#.. ..#.. ..#.. ..#.. #####",
        "J ..### ...#. ...#. ...#. ...#. #..#. .##..",
        "K #...# #..#. #.#.. ##... #.#.. #..#. #...#",
        "L #.... #.... #.... #.... #.... #.... #####",
        "M #...# ##.## #.#.# #...# #...# #...# #...#",
        "N #...# ##..# #.#.# #..## #...# #...# #...#",
        "O .###. #...# #...# #...# #...# #...# .###.",
        "P ####. #...# #...# ####. #.... #.... #....",
        "Q .###. #...# #...# #...# #.#.# #..#. .##.#",
        "R ####. #...# #...# ####. #.#.. #..#. #...#",
        "S .#### #.... #.... .###. ....# ....# ####.",
        "T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
        "U #...# #...# #...# #...# #...# #...# .###.",
        "V #...# #...# #...# #...# #...# .#.#. ..#..",
        "W #...# #...# #...# #...# #.#.# ##.## #...#",
        "X #...# #...# .#.#. ..#.. .#.#. #...# #...#",
        "Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#..",
        "Z ##### ....# ...#. ..#.. .#... #.... #####",
        "0 .###. #...# #..## #.#.# ##..# #...# .###.",
        "1 ..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.",
        "2 .###. #...# ....# ...#. ..#.. .#... #####",
        "3 ##### ...#. ..#.. ...#. ....# #...# .###.",
        "4 ...#. ..##. .#.#. #..#. ##### ...#. ...#.",
        "5 ##### #.... ####. ....# ....# #...# .###.",
        "6 ..##. .#... #.... ####. #...# #...# .###.",
        "7 ##### ....# ...#. ..#.. .#... .#... .#...",
        "8 .###. #...# #...# .###. #...# #...# .###.",
        "9 .###. #...# #...# .#### ....# ...#. .##..",
        ". ..... ..... ..... ..... ..... .##.. .##..",
        ", ..... ..... ..... ..... .##.. .##.. .#...",
        ": ..... .##.. .##.. ..... .##.. .##.. .....",
        "; ..... .##.. .##.. ..... .##.. .##.. .#...",
        "- ..... ..... ..... ##### ..... ..... .....",
        "+ ..... ..#.. ..#.. ##### ..#.. ..#.. .....",
        "= ..... ..... ##### ..... ##### ..... .....",
        "_ ..... ..... ..... ..... ..... ..... #####",
        "/ ....# ....# ...#. ..#.. .#... #.... #....",
        "\\ #.... #.... .#... ..#.. ...#. ....# ....#",
        "( ...#. ..#.. .#... .#... .#... ..#.. ...#.",
        ") .#... ..#.. ...#. ...#. ...#. ..#.. .#...",
        "[ .###. .#... .#... .#... .#... .#... .###.",
        "] ###.. ..#.. ..#.. ..#.. ..#.. ..#.. ###..",
        "< ...#. ..#.. .#... #.... .#... ..#.. ...#.",
        "> .#... ..#.. ...#. ....# ...#. ..#.. .#...",
        "? .###. #...# ....# ...#. ..#.. ..... ..#..",
        "! ..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#..",
        "% ##..# ##..# ...#. ..#.. .#... #..## #..##",
        "* ..... #.#.# .###. ##### .###. #.#.# .....",
    };

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, int> _glyphIndex = new();
    private bool _disposed;

    public BitmapFont(GraphicsDevice device)
    {
        (char[] characters, string[] bitmaps) = ParseGlyphs();
        _atlas = BuildAtlas(device, bitmaps);

        for (int i = 0; i < characters.Length; i++)
        {
            _glyphIndex[characters[i]] = i;
        }
    }

    public Texture2D Atlas => _atlas;

    /// <summary>Source rectangle of a glyph inside the atlas, or an empty rectangle when unsupported.</summary>
    public Rectangle GetSourceRect(char character)
    {
        if (!TryGetGlyph(character, out int index))
        {
            return Rectangle.Empty;
        }

        int column = index % ColumnCount;
        int row = index / ColumnCount;
        return new Rectangle(column * GlyphWidth, row * GlyphHeight, GlyphWidth, GlyphHeight);
    }

    public bool TryGetGlyph(char character, out int index)
    {
        character = char.ToUpperInvariant(character);
        return _glyphIndex.TryGetValue(character, out index);
    }

    /// <summary>Splits the glyph table into characters and their whitespace-free bitmaps.</summary>
    private static (char[] Characters, string[] Bitmaps) ParseGlyphs()
    {
        char[] characters = new char[Glyphs.Length];
        string[] bitmaps = new string[Glyphs.Length];

        for (int i = 0; i < Glyphs.Length; i++)
        {
            string entry = Glyphs[i];
            characters[i] = entry[0];
            bitmaps[i] = entry.Substring(1).Replace(" ", string.Empty);

            if (bitmaps[i].Length != GlyphWidth * GlyphHeight)
            {
                throw new InvalidOperationException(
                    $"Glyph '{characters[i]}' has {bitmaps[i].Length} cells, expected {GlyphWidth * GlyphHeight}.");
            }
        }

        return (characters, bitmaps);
    }

    private static Texture2D BuildAtlas(GraphicsDevice device, string[] bitmaps)
    {
        int rowCount = (bitmaps.Length + ColumnCount - 1) / ColumnCount;
        int atlasWidth = ColumnCount * GlyphWidth;
        int atlasHeight = rowCount * GlyphHeight;
        Color[] pixels = new Color[atlasWidth * atlasHeight];

        for (int glyph = 0; glyph < bitmaps.Length; glyph++)
        {
            string bitmap = bitmaps[glyph];
            int column = glyph % ColumnCount;
            int row = glyph / ColumnCount;

            for (int y = 0; y < GlyphHeight; y++)
            {
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if (bitmap[(y * GlyphWidth) + x] != '#')
                    {
                        continue;
                    }

                    int atlasX = (column * GlyphWidth) + x;
                    int atlasY = (row * GlyphHeight) + y;
                    pixels[(atlasY * atlasWidth) + atlasX] = Color.White;
                }
            }
        }

        Texture2D atlas = new(device, atlasWidth, atlasHeight, mipmap: false, SurfaceFormat.Color);
        atlas.SetData(pixels);
        return atlas;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _atlas.Dispose();
    }
}
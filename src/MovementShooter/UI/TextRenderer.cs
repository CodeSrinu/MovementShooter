using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MovementShooter.UI;

/// <summary>Draws strings with a <see cref="BitmapFont"/>. Development display only, not the game HUD.</summary>
public sealed class TextRenderer : IDisposable
{
    private readonly SpriteBatch _batch;
    private readonly BitmapFont _font;
    private readonly StringBuilder _line = new();
    private bool _disposed;

    public TextRenderer(GraphicsDevice device, BitmapFont font)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _batch = new SpriteBatch(device);
    }

    /// <summary>Draws one line of text with its top-left corner at the given pixel position.</summary>
    public void DrawLine(string text, Vector2 position, Color color, int scale = 1)
    {
        _batch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);

        float x = position.X;
        foreach (char character in text)
        {
            Rectangle source = _font.GetSourceRect(character);
            if (source != Rectangle.Empty)
            {
                _batch.Draw(_font.Atlas, new Rectangle((int)x, (int)position.Y, BitmapFont.GlyphWidth * scale, BitmapFont.GlyphHeight * scale), source, color);
            }

            x += (BitmapFont.GlyphWidth + 1) * scale;
        }

        _batch.End();
    }

    /// <summary>Draws several lines stacked downwards.</summary>
    public void DrawLines(string[] lines, Vector2 position, Color color, int lineSpacing = 2, int scale = 1)
    {
        float y = position.Y;
        foreach (string text in lines)
        {
            DrawLine(text, new Vector2(position.X, y), color, scale);
            y += (BitmapFont.GlyphHeight + lineSpacing) * scale;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _batch.Dispose();
        _line.Clear();
    }
}
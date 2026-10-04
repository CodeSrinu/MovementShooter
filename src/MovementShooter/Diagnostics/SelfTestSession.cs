using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.App;
using MovementShooter.Core;

namespace MovementShooter.Diagnostics;

/// <summary>
/// Automated render validation. Renders the scene into an offscreen target, writes a PNG screenshot and
/// analyses the pixels so a non-interactive machine (or an AI assistant) can confirm that 3D rendering,
/// the camera and the world builder actually produce an image.
/// </summary>
public sealed class SelfTestSession : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly AppConfig _config;
    private readonly string _outputDirectory;
    private bool _disposed;

    public SelfTestSession(GraphicsDevice device, AppConfig config)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _outputDirectory = config.ResolveOutputDirectory();

        // device, width, height, mipmap, colour format, depth format, MSAA samples, usage
        Target = new RenderTarget2D(
            device,
            config.SelfTestWidth,
            config.SelfTestHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8,
            0,
            RenderTargetUsage.DiscardContents);
    }

    public RenderTarget2D Target { get; }

    public bool IsFinished(long frameIndex) => frameIndex >= _config.SelfTestFrames;

    /// <summary>
    /// Reads the last render back from the GPU once, writes it as a PNG and returns the pixel report.
    /// </summary>
    public RenderReport Capture(long frameIndex, out string screenshotPath)
    {
        Color[] pixels = ReadPixels();

        screenshotPath = Path.Combine(
            _outputDirectory,
            string.Create(CultureInfo.InvariantCulture, $"frame-{frameIndex:000}.png"));
        PngWriter.Save(screenshotPath, _config.SelfTestWidth, _config.SelfTestHeight, ToRgba(pixels));

        return Analyse(pixels);
    }

    private Color[] ReadPixels()
    {
        Color[] pixels = new Color[_config.SelfTestWidth * _config.SelfTestHeight];
        _device.SetRenderTarget(null);
        Target.GetData(pixels);
        return pixels;
    }

    private RenderReport Analyse(Color[] pixels)
    {
        Color background = _config.ClearColor;
        int litPixels = 0;
        long redSum = 0;
        long greenSum = 0;
        long blueSum = 0;
        HashSet<int> distinctColors = new();

        foreach (Color pixel in pixels)
        {
            redSum += pixel.R;
            greenSum += pixel.G;
            blueSum += pixel.B;

            bool differsFromClear =
                Math.Abs(pixel.R - background.R) > 8 ||
                Math.Abs(pixel.G - background.G) > 8 ||
                Math.Abs(pixel.B - background.B) > 8;

            if (differsFromClear)
            {
                litPixels++;
            }

            // Quantise to 5 bits per channel so anti-aliasing does not inflate the count.
            distinctColors.Add(((pixel.R >> 3) << 10) | ((pixel.G >> 3) << 5) | (pixel.B >> 3));
        }

        int total = pixels.Length;
        if (total == 0)
        {
            return new RenderReport(0, 0, 0f, 0, Vector3.Zero);
        }

        float coverage = (float)litPixels / total;

        return new RenderReport(
            total,
            litPixels,
            coverage,
            distinctColors.Count,
            new Vector3(redSum / (float)total / 255f, greenSum / (float)total / 255f, blueSum / (float)total / 255f));
    }

    private static byte[] ToRgba(Color[] pixels)
    {
        byte[] rgba = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            rgba[(i * 4) + 0] = pixel.R;
            rgba[(i * 4) + 1] = pixel.G;
            rgba[(i * 4) + 2] = pixel.B;
            rgba[(i * 4) + 3] = pixel.A;
        }

        return rgba;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Target.Dispose();
    }
}

/// <summary>Pixel statistics for one offscreen frame.</summary>
public readonly record struct RenderReport(
    int TotalPixels,
    int NonClearPixels,
    float Coverage,
    int DistinctColors,
    Vector3 AverageColor)
{
    /// <summary>A believable 3D frame covers a decent area, has shading variety and is not uniformly dark.</summary>
    public bool LooksLikeARenderedScene =>
        TotalPixels > 0 &&
        Coverage > 0.02f &&
        DistinctColors > 8 &&
        (AverageColor.X + AverageColor.Y + AverageColor.Z) > 0.05f;

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"pixels={TotalPixels} nonClear={NonClearPixels} coverage={Coverage:P1} colors={DistinctColors} avg={AverageColor.X:0.00}/{AverageColor.Y:0.00}/{AverageColor.Z:0.00}");
}
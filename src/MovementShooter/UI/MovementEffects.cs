using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MovementShooter.Player;

namespace MovementShooter.UI;

/// <summary>
/// One short-lived quad, already oriented and faded, ready for the renderer to upload.
/// </summary>
/// <param name="Centre">World position of the quad.</param>
/// <param name="Along">Unit vector along the quad's length. This is the dash direction for a streak.</param>
/// <param name="Across">Unit vector across the quad's width, billboarded to face the camera.</param>
/// <param name="HalfLength">Half the length in metres.</param>
/// <param name="HalfWidth">Half the width in metres.</param>
/// <param name="Color">Colour with the fade already applied.</param>
public readonly record struct FeedbackQuad(
    Vector3 Centre,
    Vector3 Along,
    Vector3 Across,
    float HalfLength,
    float HalfWidth,
    Color Color);

/// <summary>
/// The pure, GPU-free part of the movement feedback: spawning short-lived quads in response to movement state,
/// ageing them, and handing them to the renderer.
///
/// It exists without any graphics dependency so the behaviour can be tested headlessly, exactly like the
/// movement itself. It holds no reference to the physics world or to any collider - it is handed a position to
/// appear at and reads movement state, and that is all. Nothing it does can affect the simulation.
/// </summary>
public sealed class MovementEffects
{
    private readonly List<LiveQuad> _live = new();

    // Deterministic per-instance jitter, so the automated checks are reproducible while a burst still varies and
    // repeated dashes do not stamp an identical pattern.
    private readonly Random _random = new(20240917);

    private sealed class LiveQuad
    {
        public Vector3 Position;
        public Vector3 Drift;

        /// <summary>Direction the quad is drawn along, fixed at spawn. For a streak this is the dash direction.</summary>
        public Vector3 Along;

        public float Age;
        public float Lifetime;
        public float HalfLength;
        public float HalfWidth;
        public float Growth;
        public Color BaseColor;
    }

    public MovementEffects(MovementFeedbackSettings? settings = null)
    {
        Settings = settings ?? new MovementFeedbackSettings();
    }

    /// <summary>
    /// The settings in force. Exposed because the renderer reads the peak alpha from here, so the simulated
    /// side and the drawn side can never disagree about what a cue should look like.
    /// </summary>
    public MovementFeedbackSettings Settings { get; }

    /// <summary>Live quads right now, for the renderer and for tests.</summary>
    public int LiveCount => _live.Count;

    /// <summary>Total quads ever spawned. Tests use this to confirm a cue actually fired.</summary>
    public int TotalSpawned { get; private set; }

    /// <summary>
    /// Advances every live quad by <paramref name="deltaSeconds"/>, retiring the ones past their lifetime.
    /// Frame-rate independent: ageing is in seconds, not in frames.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            LiveQuad quad = _live[i];
            quad.Age += deltaSeconds;

            if (quad.Age >= quad.Lifetime)
            {
                _live.RemoveAt(i);
                continue;
            }

            quad.Position += quad.Drift * deltaSeconds;
            _live[i] = quad;
        }
    }

    /// <summary>Clears every live quad without spawning anything, so a burst cannot outlive a respawn.</summary>
    public void Clear() => _live.Clear();

    /// <summary>
    /// Fires the dash cue: a spread of short streaks ahead of the player, each drawn along the direction they
    /// travelled, drifting past the camera and fading out well inside the burst.
    ///
    /// <paramref name="dashDirection"/> is the movement code's own committed dash direction, so the streaks always
    /// agree with the way the player actually went - including an air dash, and a dash with no movement input
    /// where the camera supplies the heading.
    /// </summary>
    public void SpawnDashStreaks(Vector3 eyePosition, Vector3 dashDirection)
    {
        if (_live.Count + Settings.DashStreakCount > Settings.Capacity)
        {
            return;
        }

        Vector3 forward = dashDirection.LengthSquared() > 0.0001f
            ? Vector3.Normalize(dashDirection)
            : Vector3.Forward;

        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));

        for (int i = 0; i < Settings.DashStreakCount; i++)
        {
            float t = Settings.DashStreakCount > 1 ? i / (float)(Settings.DashStreakCount - 1) : 0.5f;

            // Evenly spaced across the burst rather than random, so the group reads as one event. A little
            // jitter on each keeps it from looking like a ruler.
            float lateral = ((t - 0.5f) * 2f * Settings.DashStreakSpread) + Jitter(-0.06f, 0.06f);
            float vertical = Jitter(-Settings.DashStreakHeightSpread, Settings.DashStreakHeightSpread);

            Vector3 position = eyePosition
                + (forward * Settings.DashStreakSpawnDistance)
                + (right * lateral)
                + (Vector3.Up * vertical);

            // Stretched along the dash direction, so each one reads as a line of travel, and drifting forward
            // along the same axis so it appears to rush past the eye.
            Add(
                position,
                forward * Settings.DashStreakDriftSpeed,
                forward,
                Settings.DashStreakLength,
                Settings.DashStreakWidth,
                growth: 1f,
                Settings.DashStreakSeconds,
                Settings.DashStreakColor);
        }
    }

    /// <summary>
    /// Fires the slide cue: a few motes of dust kicked up around the feet, thrown backwards along the slide
    /// and spreading as they fade.
    /// </summary>
    public void SpawnSlideDust(Vector3 footPosition, Vector3 slideDirection)
    {
        if (_live.Count + Settings.SlideDustCount > Settings.Capacity)
        {
            return;
        }

        Vector3 backward = slideDirection.LengthSquared() > 0.0001f
            ? -Vector3.Normalize(slideDirection)
            : Vector3.Forward;

        for (int i = 0; i < Settings.SlideDustCount; i++)
        {
            float angle = MathHelper.TwoPi * (i / (float)Settings.SlideDustCount) + Jitter(-0.3f, 0.3f);
            float radius = Settings.SlideDustSpawnRadius * Jitter(0.5f, 1f);

            Vector3 position = footPosition
                + new Vector3(MathF.Cos(angle) * radius, Jitter(-0.02f, 0.03f), MathF.Sin(angle) * radius)
                + (Vector3.Up * Settings.SlideDustHeight);

            Add(
                position,
                backward * (Settings.SlideDustDriftSpeed * Jitter(0.5f, 1.2f)),
                backward,
                Settings.SlideDustSize,
                Settings.SlideDustSize,
                Settings.SlideDustGrowth,
                Settings.SlideDustSeconds,
                Settings.SlideDustColor);
        }
    }

    private void Add(
        Vector3 position,
        Vector3 drift,
        Vector3 along,
        float length,
        float width,
        float growth,
        float lifetime,
        Color color)
    {
        _live.Add(new LiveQuad
        {
            Position = position,
            Drift = drift,
            Along = along,
            Age = 0f,
            Lifetime = lifetime,
            HalfLength = length * 0.5f,
            HalfWidth = width * 0.5f,
            Growth = growth,
            BaseColor = color,
        });

        TotalSpawned++;
    }

    /// <summary>
    /// Fills <paramref name="destination"/> with the live quads, each billboarded around its length axis so it
    /// always presents its full width to the eye, and already faded. Returns how many were written.
    /// </summary>
    public int CollectQuads(Matrix view, float peakAlpha, List<FeedbackQuad> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();

        // The camera's forward, taken out of the view matrix. MonoGame's convention is row-vector, so the basis
        // vectors occupy the first three *rows* and the translation sits in the fourth - reading M13/M23/M33
        // instead picks up the translation column and yields a meaningless direction.
        //
        // Each quad's width axis is then perpendicular to both its own length and this, which is what keeps a
        // streak visible from any angle instead of disappearing edge-on as the player turns.
        Vector3 cameraForward = new(-view.M21, -view.M22, -view.M23);
        if (cameraForward.LengthSquared() < 0.0001f)
        {
            return 0;
        }

        cameraForward = Vector3.Normalize(cameraForward);

        foreach (LiveQuad quad in _live)
        {
            float life = Math.Clamp(1f - (quad.Age / quad.Lifetime), 0f, 1f);

            // Fade with the square of the remaining life. A linear fade spends most of its time nearly invisible,
            // so the cue arrives as a flash and then blinks out; squaring keeps it readable for most of its life
            // and still reaches exactly zero at the end.
            float fade = life * life;

            Color color = quad.BaseColor;
            color.A = (byte)Math.Clamp(peakAlpha * fade * (color.A / 255f) * 255f, 0f, 255f);

            if (color.A == 0)
            {
                continue;
            }

            Vector3 along = quad.Along.LengthSquared() > 0.0001f ? Vector3.Normalize(quad.Along) : Vector3.Forward;
            Vector3 across = Vector3.Cross(along, cameraForward);

            if (across.LengthSquared() < 0.0001f)
            {
                // Looking straight along the streak: no width axis exists, so there is nothing to draw.
                continue;
            }

            // Motes grow as they fade, which is what makes dust look like it is dispersing rather than shrinking.
            float growth = 1f + ((quad.Growth - 1f) * (1f - life));

            destination.Add(new FeedbackQuad(
                quad.Position,
                along,
                Vector3.Normalize(across),
                quad.HalfLength * growth,
                quad.HalfWidth * growth,
                color));
        }

        return destination.Count;
    }

    private float Jitter(float minimum, float maximum) =>
        minimum + ((float)_random.NextDouble() * (maximum - minimum));
}

using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.UI;

/// <summary>
/// Every number the procedural movement feedback uses. Kept apart from <c>PlayerTuning</c> deliberately: none
/// of this is gameplay, and a value here can be changed without any risk to how the player moves.
/// </summary>
/// <remarks>
/// The whole system is two fixed-size pools of short-lived quads. There is no emitter, no asset, no content
/// pipeline and no per-frame allocation, so it stays cheap enough to leave switched on during play.
/// </remarks>
public sealed class MovementFeedbackSettings
{
    // --------------------------------------------------------------- dash streaks

    /// <summary>How many streaks a single dash throws off.</summary>
    public int DashStreakCount { get; set; } = 9;

    /// <summary>Seconds a streak lives. Short by design: this is a flash of speed, not a trail.</summary>
    public float DashStreakSeconds { get; set; } = 0.11f;

    /// <summary>How far in front of the player a streak is spawned, in metres.</summary>
    public float DashStreakSpawnDistance { get; set; } = 1.5f;

    /// <summary>Half-width of a streak in metres. Thin enough to read as a line rather than a slab.</summary>
    public float DashStreakWidth { get; set; } = 0.022f;

    /// <summary>Length of a streak in metres, along the dash direction.</summary>
    public float DashStreakLength { get; set; } = 1.5f;

    /// <summary>
    /// How far the spawn point spreads either side of the dash line, in metres. Gives the burst some width so
    /// it reads as a rush of speed rather than a single line through the middle of the screen.
    /// </summary>
    public float DashStreakSpread { get; set; } = 0.75f;

    /// <summary>Vertical spread of the spawn, in metres, centred on the camera's eye height.</summary>
    public float DashStreakHeightSpread { get; set; } = 0.5f;

    /// <summary>Colour at the moment a streak appears. Alpha here is the peak of the fade.</summary>
    public Color DashStreakColor { get; set; } = new(232, 244, 255, 255);

    /// <summary>Speed at which streaks slide past the camera, in metres per second.</summary>
    public float DashStreakDriftSpeed { get; set; } = 14f;

    // ------------------------------------------------------------------ slide dust

    /// <summary>How many motes of dust a slide kicks up.</summary>
    public int SlideDustCount { get; set; } = 5;

    /// <summary>Seconds a dust mote lives.</summary>
    public float SlideDustSeconds { get; set; } = 0.22f;

    /// <summary>How far from the player's centre each mote starts, in metres.</summary>
    public float SlideDustSpawnRadius { get; set; } = 0.28f;

    /// <summary>Height above the floor that the dust is thrown from, in metres.</summary>
    public float SlideDustHeight { get; set; } = 0.06f;

    /// <summary>Size of a mote in metres, growing as it fades.</summary>
    public float SlideDustSize { get; set; } = 0.11f;

    /// <summary>How much bigger a mote is at the end of its life than at the start.</summary>
    public float SlideDustGrowth { get; set; } = 1.9f;

    /// <summary>How far a mote drifts backwards along the slide, in metres per second.</summary>
    public float SlideDustDriftSpeed { get; set; } = 1.6f;

    /// <summary>Colour of the dust. Muted and close to the ground's own tone, so it is a hint not a cloud.</summary>
    public Color SlideDustColor { get; set; } = new(198, 192, 178, 255);

    /// <summary>Peak alpha for both kinds of effect. Both fade out from here; nothing ever gets brighter.</summary>
    public float PeakAlpha { get; set; } = 0.85f;

    /// <summary>
    /// Upper bound on live quads, so a burst can never grow the pools. Matches the ceiling the renderer sizes
    /// its buffers from.
    /// </summary>
    public int Capacity { get; set; } = MovementFeedbackSettingsDefaults.MaxQuads;
}

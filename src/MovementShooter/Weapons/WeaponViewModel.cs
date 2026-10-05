using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Weapons;

/// <summary>
/// First-person presentation state for a weapon: where the model sits relative to the eye, and how it reacts to
/// firing.
///
/// Deliberately pure - no graphics device, no engine state, no reference to the camera's internals. It owns a
/// recoil clock and answers "where should the model be this frame". That makes the whole of the weapon's
/// <b>presentation</b> testable headlessly, exactly like the movement code, which is the only reason the recoil
/// timing and the muzzle flash can be checked by an automated test rather than by eye.
/// </summary>
public sealed class WeaponViewModel
{
    private readonly ViewModelTuning _tuning;

    /// <summary>Seconds since the last shot, used to drive the recoil spring.</summary>
    private float _sinceFired = float.PositiveInfinity;

    public WeaponViewModel(ViewModelTuning tuning, float idleSeconds = 1f)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _sinceFired = idleSeconds;
    }

    /// <summary>Called when a shot actually goes off. Assigning rather than accumulating, so a burst cannot stack.</summary>
    public void OnFired() => _sinceFired = 0f;

    /// <summary>Seconds since the last shot; positive infinity if none has been fired.</summary>
    public float TimeSinceFired => _sinceFired;

    /// <summary>True while the muzzle flash should still be drawn.</summary>
    public bool IsFlashing => _sinceFired <= _tuning.MuzzleFlashSeconds;

    /// <summary>
    /// Recoil displacement in metres: back along the barrel and up, springing back to rest.
    ///
    /// A damped cosine rather than a spring solver: it starts at full extent, returns through rest, and overshoots
    /// slightly before settling, which is what a launcher does. Being a function of the time since the shot also
    /// makes it frame-rate independent for free - there is no per-frame integration to get wrong, which matters
    /// because a mouse button can be held and this would otherwise be integrated at the frame rate.
    /// </summary>
    public Vector3 RecoilOffset
    {
        get
        {
            float t = NormalisedRecoilTime;
            if (t >= 1f)
            {
                return Vector3.Zero;
            }

            // Decaying amplitude, times a cosine that produces one overshoot on the way back.
            float amount = MathF.Exp(-4.5f * t) * MathF.Cos(t * MathHelper.Pi * 2.2f);

            return new Vector3(0f, amount * _tuning.RecoilRise, -amount * _tuning.RecoilBack);
        }
    }

    /// <summary>
    /// Recoil rotation in radians: muzzle up, then settling. Same shape as the displacement, so the model does not
    /// appear to slide without pivoting about its grip.
    /// </summary>
    public Vector3 RecoilRotation
    {
        get
        {
            float t = NormalisedRecoilTime;
            if (t >= 1f)
            {
                return Vector3.Zero;
            }

            float amount = MathF.Exp(-4.5f * t) * MathF.Cos(t * MathHelper.Pi * 2.2f);

            return new Vector3(amount * _tuning.RecoilPitchRadians, 0f, amount * _tuning.RecoilYawRadians);
        }
    }

    /// <summary>
    /// How far through the recoil animation we are: 0 at the instant of the shot, 1 when it has settled. Clamped
    /// rather than looping, so the model always comes to rest at the pose it started from.
    /// </summary>
    public float NormalisedRecoilTime =>
        _tuning.RecoilSeconds <= 0f
            ? 1f
            : Math.Clamp(_sinceFired / _tuning.RecoilSeconds, 0f, 1f);

    /// <summary>
    /// Muzzle flash strength, 1 at the instant of firing and 0 when there is no flash. Scales the flash geometry as
    /// well as its alpha, so the flash collapses as well as fades.
    /// </summary>
    public float MuzzleFlashStrength
    {
        get
        {
            if (_tuning.MuzzleFlashSeconds <= 0f || !IsFlashing)
            {
                return 0f;
            }

            float t = _sinceFired / _tuning.MuzzleFlashSeconds;
            return Math.Clamp(1f - (t * t), 0f, 1f);
        }
    }

    /// <summary>Advances the recoil clock by one frame.</summary>
    public void Update(float deltaSeconds)
    {
        if (float.IsFinite(_sinceFired))
        {
            _sinceFired += deltaSeconds;
        }
    }
}

/// <summary>
/// How the first-person weapon model is held, and how it reacts to firing. Presentation only: every value is a
/// distance or an angle, and none of it can affect the simulation.
/// </summary>
public sealed class ViewModelTuning
{
    /// <summary>
    /// Where the model sits relative to the eye, in metres: to the right, down, and forward.
    ///
    /// Tuned by eye against the viewmodel's 62 degree field of view. What matters is that the launcher occupies
    /// the lower right of the screen and leaves the centre clear, because the centre is where the player aims.
    /// Anything reaching the middle reads as scenery rather than as something being held.
    /// </summary>
    public Vector3 RestOffset { get; set; } = new(0.30f, -0.25f, 0.42f);

    /// <summary>
    /// Uniform scale for the whole model. One value so the proportions can be adjusted together instead of by
    /// hand-editing seven part sizes: the parts are laid out in metres, and this decides how large the launcher
    /// actually reads on screen.
    /// </summary>
    public float ModelScale { get; set; } = 0.85f;

    /// <summary>
    /// Rest tilt of the model in view space, in radians: X pitches the muzzle up slightly, Y brings the muzzle
    /// towards the centre of the screen, Z rolls it.
    ///
    /// Applied <i>after</i> the recoil and inside the view basis, so it is a fixed offset between the model and
    /// the aim direction rather than something that compounds as the player turns. A launcher held off to the right
    /// reads as aimed slightly inward, which is what stops it looking like it is pointing somewhere else entirely.
    /// </summary>
    public Vector3 RestRotation { get; set; } = new(0.04f, -0.17f, 0.03f);

    /// <summary>
    /// Field of view for the viewmodel, in degrees. Narrower than the world's 90 on purpose: at that field of view
    /// a held object is in such strong perspective that it looks enormous and its edges bend. A separate projection
    /// is the standard fix and costs one matrix.
    /// </summary>
    public float ViewModelFieldOfViewDegrees { get; set; } = 62f;

    /// <summary>Near plane for the viewmodel projection, in metres. Small, because the model is held very close.</summary>
    public float ViewModelNearPlane { get; set; } = 0.01f;

    /// <summary>Far plane for the viewmodel projection, in metres. Very small: nothing else is drawn with it.</summary>
    public float ViewModelFarPlane { get; set; } = 5f;

    /// <summary>Seconds the recoil animation takes to settle.</summary>
    public float RecoilSeconds { get; set; } = 0.28f;

    /// <summary>How far the model slides backwards along the barrel at the peak of the recoil, in metres.</summary>
    public float RecoilBack { get; set; } = 0.045f;

    /// <summary>How far the model rises at the peak of the recoil, in metres.</summary>
    public float RecoilRise { get; set; } = 0.016f;

    /// <summary>Muzzle rotation at the peak of the recoil, in radians. Positive pitches the muzzle up.</summary>
    public float RecoilPitchRadians { get; set; } = 0.16f;

    /// <summary>Slight sideways rotation at the peak of the recoil, in radians.</summary>
    public float RecoilYawRadians { get; set; } = 0.03f;

    /// <summary>Seconds the muzzle flash is visible for.</summary>
    public float MuzzleFlashSeconds { get; set; } = 0.07f;

    /// <summary>Size of the muzzle flash at full strength, in metres.</summary>
    public float MuzzleFlashSize { get; set; } = 0.16f;
}

/// <summary>
/// The view and projection for a first-person weapon, plus the world matrix that places it.
///
/// Held separately from the camera on purpose. A viewmodel needs its own view and projection - a narrower field
/// of view, and a near plane in centimetres rather than metres - and mixing them with the world's is exactly what
/// makes a held weapon either distort or disappear. Keeping them here means the whole first-person pass is
/// assembled in one place and nothing else has to know about it.
/// </summary>
public sealed class ViewModelRenderState
{
    public ViewModelRenderState(WeaponViewModel viewmodel, ViewModelTuning tuning)
    {
        Viewmodel = viewmodel ?? throw new ArgumentNullException(nameof(viewmodel));
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
    }

    public WeaponViewModel Viewmodel { get; }

    public ViewModelTuning Tuning { get; }

    /// <summary>Camera basis for the viewmodel: right, up and forward, with no world translation.</summary>
    public Matrix View { get; set; } = Matrix.Identity;

    /// <summary>Narrow field of view with a centimetre-scale near plane.</summary>
    public Matrix Projection { get; set; } = Matrix.Identity;

    /// <summary>
    /// Places the model in view space: the camera basis, with the hold position and the current recoil applied as a
    /// translation, and the whole model scaled.
    ///
    /// Only a position, no rotation. The facing comes entirely from the camera's view matrix; baking a yaw into
    /// the model as well would double it up and the launcher would swing wider than the aim as the player turns.
    /// </summary>
    public Matrix GetWorldMatrix() =>
        Matrix.CreateScale(Tuning.ModelScale)
        * Matrix.CreateTranslation(Tuning.RestOffset + Viewmodel.RecoilOffset);
}
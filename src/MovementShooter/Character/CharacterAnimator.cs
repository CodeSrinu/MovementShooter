using System;
using Microsoft.Xna.Framework;

namespace MovementShooter.Character;

/// <summary>Which clip the character should be playing.</summary>
public enum CharacterAnim
{
    Idle,
    Run,
    Jump,
    Fall,
    Land,
    Slide,
    Dash,
    Fire,
}

/// <summary>
/// Everything the character needs to know about the world for one frame, as plain facts.
///
/// Deliberately carries no Player, PhysicsWorld or camera reference. The character is
/// presentation, and the rule the rest of the project already holds - that feedback must
/// report on the simulation and never touch it - is only enforceable if this type cannot
/// reach back into the thing it is describing.
/// </summary>
/// <param name="IsGrounded">Whether the physics capsule is supported.</param>
/// <param name="VerticalSpeed">Signed Y speed, used to tell rising from falling.</param>
/// <param name="HorizontalSpeed">Ground speed in m/s, used to drive the run cadence.</param>
/// <param name="IsSliding">Whether the movement state is Sliding.</param>
/// <param name="IsDashing">Whether a dash is in progress.</param>
/// <param name="FireTriggered">True on the single frame a shot actually left the barrel.</param>
public readonly record struct CharacterAnimationInput(
    bool IsGrounded,
    float VerticalSpeed,
    float HorizontalSpeed,
    bool IsSliding,
    bool IsDashing,
    bool FireTriggered);

/// <summary>
/// Drives one character: chooses a clip, advances its time, and writes the resulting pose
/// into a <see cref="CharacterSkeleton"/>.
///
/// Holds no graphics state, so the headless checks can run the whole state machine without
/// a device - which is the only reason clip selection can be verified at all.
///
/// Clips are in place: translation is owned by physics, and the only translation these
/// clips carry is the vertical bob and crouch the poses were authored with. Nothing here
/// ever adds world position to the skeleton; the renderer places the result.
/// </summary>
public sealed class CharacterAnimator
{
    private readonly CharacterAsset _asset;
    private readonly int[] _clipIndexByState;
    private readonly CharacterSkeleton _skeletonA;
    private readonly CharacterSkeleton _skeletonB;

    private int _currentState = -1;
    private int _previousState = -1;
    private float _time;
    private float _blendRemaining;
    private float _blendDuration;

    private bool _wasGrounded = true;
    private bool _wasSliding;
    private bool _wasDashing;

    public CharacterAnimator(CharacterAsset asset, CharacterSkeleton poseTarget, CharacterSkeleton blendScratch)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(poseTarget);
        ArgumentNullException.ThrowIfNull(blendScratch);

        _asset = asset;
        _skeletonA = poseTarget;
        _skeletonB = blendScratch;

        _clipIndexByState = new int[8];
        for (int i = 0; i < _clipIndexByState.Length; i++)
        {
            _clipIndexByState[i] = -1;
        }

        _clipIndexByState[(int)CharacterAnim.Idle] = Require(asset.IndexOfClip("KINETIC_Idle"));
        _clipIndexByState[(int)CharacterAnim.Run] = Require(asset.IndexOfClip("KINETIC_Run"));
        _clipIndexByState[(int)CharacterAnim.Jump] = Require(asset.IndexOfClip("KINETIC_Jump"));
        _clipIndexByState[(int)CharacterAnim.Fall] = Require(asset.IndexOfClip("KINETIC_Fall"));
        _clipIndexByState[(int)CharacterAnim.Land] = Require(asset.IndexOfClip("KINETIC_Land"));
        _clipIndexByState[(int)CharacterAnim.Slide] = Require(asset.IndexOfClip("KINETIC_Slide"));
        _clipIndexByState[(int)CharacterAnim.Dash] = Require(asset.IndexOfClip("KINETIC_Dash"));
        _clipIndexByState[(int)CharacterAnim.Fire] = Require(asset.IndexOfClip("KINETIC_Fire"));

        _previousState = (int)CharacterAnim.Idle;
        _currentState = (int)CharacterAnim.Idle;
    }

    /// <summary>State currently being played.</summary>
    public CharacterAnim Current => (CharacterAnim)_currentState;

    /// <summary>State being blended out of, or the same as <see cref="Current"/> when settled.</summary>
    public CharacterAnim Previous => (CharacterAnim)_previousState;

    /// <summary>Seconds into the current clip.</summary>
    public float Time => _time;

    /// <summary>True while a cross-fade is in progress.</summary>
    public bool IsBlending => _blendRemaining > 0f;

    /// <summary>Clip playback rate currently applied, after speed scaling.</summary>
    public float PlaybackRate { get; private set; } = 1f;

    /// <summary>Clip index for a state, or -1 if the asset lacks it.</summary>
    public int ClipIndexFor(CharacterAnim state) => _clipIndexByState[(int)state];

    /// <summary>
    /// Advances one frame and leaves the posed skeleton in <see cref="Target"/>.
    /// </summary>
    public void Update(float deltaSeconds, in CharacterAnimationInput input, float runSpeedForFullRate,
        float blendSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);

        int next = ChooseState(input);

        if (next != _currentState)
        {
            _previousState = _currentState;
            _currentState = next;
            _time = 0f;
            StartBlend(blendSeconds);
        }

        PlaybackRate = RateFor((CharacterAnim)_currentState, input, runSpeedForFullRate);

        CharacterClip clip = _asset.Clips[_clipIndexByState[_currentState]];
        _time += deltaSeconds * PlaybackRate;

        if (Looping((CharacterAnim)_currentState))
        {
            if (_time >= clip.Duration && clip.Duration > 1e-4f)
            {
                _time %= clip.Duration;
            }
        }
        else if (_time > clip.Duration)
        {
            _time = clip.Duration;
        }

        // B receives the incoming pose. A deliberately keeps last frame's locals until
        // they are blended away, which is what gives the cross-fade something to start
        // from - overwriting A first would leave nothing to fade out of.
        Evaluate(_skeletonB, clip, _time);

        if (_blendRemaining > 0f)
        {
            _blendRemaining = MathF.Max(0f, _blendRemaining - deltaSeconds);
            float progress = _blendDuration > 1e-4f ? 1f - (_blendRemaining / _blendDuration) : 1f;
            _skeletonA.BlendLocalFrom(_skeletonB, progress);
        }
        else
        {
            _skeletonA.CopyLocalFrom(_skeletonB);
        }

        _skeletonA.Compose();

        _wasGrounded = input.IsGrounded;
        _wasSliding = input.IsSliding;
        _wasDashing = input.IsDashing;
    }

    /// <summary>Skeleton holding the posed result after <see cref="Update"/>.</summary>
    public CharacterSkeleton Target => _skeletonA;

    /// <summary>
    /// Poses one specific clip at one specific time into <paramref name="target"/>.
    ///
    /// Exposed for the offscreen preview, which has to sample an arbitrary point in an
    /// arbitrary clip and cannot get there by advancing time. Kept separate from
    /// <see cref="Update"/> so it cannot influence playback: it writes only the skeleton
    /// it is handed and touches no animator state.
    /// </summary>
    public void PoseClip(int clipIndex, float time, CharacterSkeleton target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if ((uint)clipIndex >= (uint)_asset.Clips.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(clipIndex), clipIndex, "No such clip.");
        }

        Evaluate(target, _asset.Clips[clipIndex], MathF.Max(0f, time));
        target.Compose();
    }

    /// <summary>
    /// Maps the current frame's facts onto a clip.
    ///
    /// One-shot actions outrank continuous states, and among them the shortest-lived wins:
    /// a shot must be visible even mid-slide, and a dash must not be hidden by the slide
    /// it interrupted. Rising and falling are separated by vertical speed rather than by
    /// "not grounded", because the apex of a jump belongs to neither and would otherwise
    /// flicker between two clips every frame.
    /// </summary>
    private int ChooseState(in CharacterAnimationInput input)
    {
        if (input.FireTriggered)
        {
            return (int)CharacterAnim.Fire;
        }

        // Dash and slide are held, not edge-triggered. Both last a fixed span of
        // movement time, so gating them on the transition alone played one frame of
        // animation and then fell through to the run cycle for the rest of the dash.
        if (input.IsDashing)
        {
            return (int)CharacterAnim.Dash;
        }

        if (!_wasGrounded && input.IsGrounded)
        {
            return (int)CharacterAnim.Land;
        }

        // Still sliding after the slide clip has played out: hold it rather than
        // snapping to a run, which would read as standing up mid-slide.
        if (input.IsSliding)
        {
            return (int)CharacterAnim.Slide;
        }

        if (!input.IsGrounded)
        {
            return input.VerticalSpeed > 0f ? (int)CharacterAnim.Jump : (int)CharacterAnim.Fall;
        }

        return input.HorizontalSpeed > RunThreshold ? (int)CharacterAnim.Run : (int)CharacterAnim.Idle;
    }

    /// <summary>Below this ground speed the character idles instead of running.</summary>
    private const float RunThreshold = 0.35f;

    private static bool Looping(CharacterAnim state) => state
        is CharacterAnim.Idle or CharacterAnim.Run or CharacterAnim.Fall;

    private float RateFor(CharacterAnim state, in CharacterAnimationInput input, float runSpeedForFullRate)
    {
        if (state != CharacterAnim.Run || runSpeedForFullRate <= 1e-3f)
        {
            return 1f;
        }

        // Scale the stride with ground speed so the feet are not obviously skating.
        // Clamped: unclamped, a rocket-jump landing at 25 m/s would spin the legs.
        float rate = input.HorizontalSpeed / runSpeedForFullRate;
        return Math.Clamp(rate, 0.55f, 2.2f);
    }

    private void StartBlend(float seconds)
    {
        _blendDuration = MathF.Max(0f, seconds);
        _blendRemaining = _blendDuration;
    }

    /// <summary>
    /// Writes a clip sample into a skeleton.
    ///
    /// Samples the stored uniform grid and rebuilds each bone's local transform. Key data
    /// is bone-major within a frame, so the whole skeleton is one linear pass.
    /// </summary>
    private void Evaluate(CharacterSkeleton skeleton, CharacterClip clip, float time)
    {
        int frameCount = clip.FrameCount;
        if (frameCount == 0)
        {
            skeleton.Reset();
            return;
        }

        float clamped = Math.Clamp(time, clip.Times[0], clip.Times[frameCount - 1]);

        int frame = 0;
        while (frame < frameCount - 1 && clip.Times[frame + 1] <= clamped)
        {
            frame++;
        }

        int next = Math.Min(frame + 1, frameCount - 1);
        float span = clip.Times[next] - clip.Times[frame];
        float t = span <= 1e-6f ? 0f : (clamped - clip.Times[frame]) / span;

        int boneCount = skeleton.BoneCount;
        for (int bone = 0; bone < boneCount; bone++)
        {
            Vector3 translation = Lerp3(clip.Translation, frame, next, boneCount, bone, t);
            Quaternion rotation = Quaternion.Slerp(
                ReadRotation(clip.Rotation, frame, boneCount, bone),
                ReadRotation(clip.Rotation, next, boneCount, bone),
                t);

            // Row-vector: rotate about the bone origin first, then move to it.
            skeleton.SetLocal(bone, Matrix.CreateFromQuaternion(rotation) * Matrix.CreateTranslation(translation));
        }
    }

    private static Quaternion ReadRotation(float[] rotation, int frame, int boneCount, int bone)
    {
        int at = ((frame * boneCount) + bone) * 4;
        return new Quaternion(rotation[at], rotation[at + 1], rotation[at + 2], rotation[at + 3]);
    }

    private static Vector3 Lerp3(float[] translation, int frame, int next, int boneCount, int bone, float t)
    {
        int a = ((frame * boneCount) + bone) * 3;
        int b = ((next * boneCount) + bone) * 3;
        return new Vector3(
            translation[a] + ((translation[b] - translation[a]) * t),
            translation[a + 1] + ((translation[b + 1] - translation[a + 1]) * t),
            translation[a + 2] + ((translation[b + 2] - translation[a + 2]) * t));
    }

    private static int Require(int clipIndex)
    {
        return clipIndex >= 0
            ? clipIndex
            : throw new InvalidOperationException("The character asset is missing a required clip.");
    }
}

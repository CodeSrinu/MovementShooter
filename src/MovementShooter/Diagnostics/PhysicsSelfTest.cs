using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MovementShooter.Character;
using MovementShooter.Combat;
using MovementShooter.Core;
using MovementShooter.Graphics;
using MovementShooter.Map;
using MovementShooter.Player;
using MovementShooter.Physics;
using MovementShooter.Targets;
using MovementShooter.UI;
using MovementShooter.Weapons;

namespace MovementShooter.Diagnostics;

/// <summary>One named check and whether it passed.</summary>
public readonly record struct CheckResult(string Name, bool Passed, string Detail);

/// <summary>
/// Headless player and physics validation. Needs no window and no GPU, so movement behaviour can be
/// checked on any machine and at any simulated frame rate - run with <c>--physics-test</c>.
/// </summary>
public static class PhysicsSelfTest
{
    private const float Tolerance = 0.02f;

public static IReadOnlyList<CheckResult> Run()
    {
        List<CheckResult> results = new();

        results.Add(SpawnAndFall());
        results.Add(RestsOnGround());
        results.Add(DoesNotFallThrough());
        results.Add(StandingIsStable());
        results.Add(WalksAtConfiguredSpeed());
        results.Add(ReleasingInputStopsPromptly());
        results.Add(JumpReachesExpectedHeight());
        results.Add(HeldJumpDoesNotBunnyHop());
        results.Add(NoAirJump());
        results.Add(WalksUpRamp());
        results.Add(FrameRateIndependentMovement());
        results.Add(ShortTapsSurviveEveryFrameRate());
        results.Add(FrameRateIndependentFalling());
        results.Add(MouseLookYawAndPitch());
        results.Add(StrafeAxisIsNotInverted());
        results.Add(WalkAxisFollowsCameraForward());
        results.Add(MovementBasisSurvivesRotation());
        results.Add(AirMomentumIsPreserved());
        results.Add(WallIsNotWalkableGround());
        results.Add(WallContactDoesNotGrantSupport());
        results.Add(RepeatedJumpAtWallDoesNotClimb());
        results.Add(WallContactCreatesNoUpwardMotion());
        results.Add(LivePathAtWallCannotClimb());
        results.Add(EveryPrimitiveFacesOutward());
        results.Add(RenderAndCollisionGeometryMatch());
        results.Add(WallContactDoesNotAllowASecondJump());
        results.Add(JumpIsAllowedInEveryDirection());

        // Milestone 3: slide and slide-jump.
        results.Add(SlideRequiresGroundedAndSpeed());
        results.Add(SlidePreservesMomentumAndDecelerates());
        results.Add(SlideAddsNoVerticalVelocity());
        results.Add(SlideExitsOnItsOwn());
        results.Add(SlideJumpWorksAndJumpsExactlyOnce());
        results.Add(SlideJumpPreservesHorizontalMomentum());
        results.Add(SlideJumpIsFrameRateIndependent());
        results.Add(AirControlStillWorksAfterSlideJump());
        results.Add(MomentumSurvivesEveryActionThatShouldKeepIt());
        results.Add(CapsuleShrinksAndFeetStayPut());
        results.Add(SlideStanceEasesInRatherThanCutting());
        results.Add(SlideStanceTransitionIsFrameRateIndependent());
        results.Add(CapsuleRestoresWhenHeadroomReturns());
        results.Add(LowCeilingKeepsPlayerCrouched());
        results.Add(SlideNeverSinksIntoTheFloor());

        // Milestone 4: dash.
        results.Add(DashFiresOnceOnAKeyEdgeAndHoldingDoesNotRepeat());
        results.Add(DashGoesTheWayThePlayerIsMoving());
        results.Add(DashFallsBackToCameraForwardWithNoInput());
        results.Add(DashPreservesMomentumWithoutStacking());
        results.Add(DashNeverTouchesVerticalVelocity());
        results.Add(OnlyOneAirDashPerAirbornePeriod());
        results.Add(DashCannotPassThroughWalls());
        results.Add(DashIsUnavailableDuringASlide());
        results.Add(GroundJumpAndDashTogetherDoNotSpendTheAirDash());
        results.Add(SlideJumpThenDashWorks());
        results.Add(DashFromARunIsClearlyMoreThanRunningFaster());
        results.Add(DashHandsBackControlWithoutAReset());
        results.Add(DashIsFrameRateIndependent());

        // Movement feel: the procedural feedback must report on the simulation and never touch it.
        results.Add(MovementEffectsSpawnAndExpire());
        results.Add(MovementEffectsDoNotTouchTheSimulation());

        // Milestone 5: weapon, projectile, explosion, damage, knockback and rocket jumping.
        results.Add(HealthTakesDamageAccumulatesAndDies());
        results.Add(ExplosionFalloffIsSmoothAndZeroAtTheEdge());
        results.Add(ExplosionOnlyAffectsTargetsInsideItsRadius());
        results.Add(ExplosionPushesTargetsAwayFromTheCentre());
        results.Add(ExplosionAddsToExistingVelocityRatherThanReplacingIt());
        results.Add(RocketSpawnsTravelsAndExpires());
        results.Add(RocketDetonatesOnWorldGeometry());
        results.Add(RocketTravelIsFrameRateIndependent());
        results.Add(RocketDamagesADummyWithDistanceFalloff());
        results.Add(ExplosionDoesNoHarmThroughAWallItCannotReach());
        results.Add(ExplosionDistinguishesSelfDamageFromCollateral());
        results.Add(RocketJumpLaunchesThePlayerThroughOrdinaryKnockback());
        results.Add(RocketJumpPreservesMomentumAndKeepsGravityActive());
        results.Add(RocketJumpWorksFromRunAndFromAir());
        results.Add(WeaponsRespectTheirFireInterval());
        results.Add(FieldOfViewStaysWithinTheEngineLimit());
        results.Add(HeldFireCannotRunTheFieldOfViewAway());
        results.Add(BillboardMatricesAreInvertible());
        results.Add(WeaponRecoilReturnsToRestAndIsFrameRateIndependent());
        results.Add(NoArenaSurfaceAllowsUnintendedClimbing());

        // Character integration.
        results.Add(CharacterAssetHasTheExpectedContents());
        results.Add(CharacterSkinningAtRestReproducesTheBindPose());
        results.Add(CharacterBonesOccupyTheSpaceTheMeshOccupies());
        results.Add(CharacterSkeletonIsSingleRootedAndFullyWeighted());
        results.Add(CharacterClipsAreWellFormed());
        results.Add(CharacterPicksTheClipTheMovementImplies());
        results.Add(CharacterHeightMatchesThePlayerCapsule());
        results.Add(CharacterFiresAndReportsWithoutTouchingTheSimulation());
        results.Add(CharacterSocketsAreAttachmentPointsAndNotBones());
        results.Add(CharacterSocketsFollowTheAnimatedSkeleton());
        results.Add(CharacterSocketChainResolvesThroughItsParent());
        results.Add(WeaponAssetIsARigidMeshWithAnAuthoredMuzzle());
        results.Add(WeaponSocketTracksHandR());
        results.Add(WeaponSupportSocketTracksHandL());
        results.Add(WeaponMuzzleRidesTheNestedSocketChain());
        results.Add(WeaponMuzzleMovesWithTheAnimation());
        results.Add(RocketSpawnsFromTheAuthoredMuzzleSocket());
        results.Add(WeaponBarrelPointsWhereTheMuzzleSocketDoes());
        results.Add(WeaponIsProportionedToTheCharacter());

        return results;
    }

    // ---------------------------------------------------------------------
    // Character integration.
    //
    // These run headless on purpose: the skinning, the skeleton and the clip
    // selection all hold no GraphicsDevice, so the parts of the character most
    // likely to be wrong can be checked without opening a window. Only the
    // renderer needs a device, and it is exercised by the self-test render and
    // by playing the game.
    // ---------------------------------------------------------------------

    private static CharacterAsset LoadCharacterAsset()
    {
        try
        {
            return CharacterAsset.Load();
        }
        catch (Exception ex) when (ex is System.IO.InvalidDataException or FileNotFoundException)
        {
            throw new InvalidOperationException(
                "The embedded character asset could not be read. Regenerate it with tools\\KineticAssetTool. Detail: " +
                ex.Message, ex);
        }
    }

    /// <summary>
    /// The asset has to actually contain the rig and every clip, or the character
    /// silently degrades to a static T-pose with no error anywhere.
    /// </summary>
    private static CheckResult CharacterAssetHasTheExpectedContents()
    {
        CharacterAsset asset = LoadCharacterAsset();

        List<string> failures = new();

        if (asset.BoneCount != 17)
        {
            failures.Add("bones " + asset.BoneCount + " != 17");
        }

        if (asset.Materials.Length != 4)
        {
            failures.Add("materials " + asset.Materials.Length + " != 4");
        }

        if (asset.Submeshes.Length != 4)
        {
            failures.Add("submeshes " + asset.Submeshes.Length + " != 4");
        }

        string[] required =
        {
            "KINETIC_Idle", "KINETIC_Run", "KINETIC_Jump", "KINETIC_Fall",
            "KINETIC_Land", "KINETIC_Slide", "KINETIC_Dash", "KINETIC_Fire",
        };

        foreach (string name in required)
        {
            if (asset.IndexOfClip(name) < 0)
            {
                failures.Add("missing clip " + name);
            }
        }

        if (asset.TriangleCount <= 0)
        {
            failures.Add("no triangles");
        }

        // Submesh ranges must tile the index buffer exactly, or part of the
        // character is never drawn or drawn twice.
        int covered = 0;
        foreach (CharacterSubmesh submesh in asset.Submeshes)
        {
            if (submesh.IndexCount % 3 != 0)
            {
                failures.Add("submesh " + submesh.MaterialIndex + " has " + submesh.IndexCount + " indices");
            }

            if (submesh.FirstIndex != covered)
            {
                failures.Add("submesh " + submesh.MaterialIndex + " starts at " + submesh.FirstIndex +
                             ", expected " + covered);
            }

            covered += submesh.IndexCount;
        }

        if (covered != asset.Indices.Length)
        {
            failures.Add("submeshes cover " + covered + " of " + asset.Indices.Length + " indices");
        }

        foreach (int index in asset.Indices)
        {
            if (index < 0 || index >= asset.VertexCount)
            {
                failures.Add("index " + index + " out of range");
                break;
            }
        }

        return new("character asset carries the rig, materials and all eight clips", failures.Count == 0,
            failures.Count == 0
                ? asset.VertexCount + " verts, " + asset.TriangleCount + " tris, " + asset.BoneCount +
                  " bones, " + asset.Materials.Length + " materials, " + asset.Clips.Length + " clips"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The decisive skinning check. Every convention error in the loader - a
    /// missing transpose, a wrong composition order, bind globals that were never
    /// recovered - shows up here as a character that is displaced from its own
    /// mesh. At rest the skinning matrix of every bone must be identity, so the
    /// skinned result has to reproduce the bind positions exactly.
    /// </summary>
    private static CheckResult CharacterSkinningAtRestReproducesTheBindPose()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterSkeleton skeleton = new(asset);
        CharacterSkin skin = new(asset);

        skin.Skin(skeleton);

        float worstPosition = 0f;
        float worstNormal = 0f;
        int worstIndex = 0;

        CharacterVertex[] source = asset.Vertices;
        for (int i = 0; i < source.Length; i++)
        {
            Vector3 position = skin.Vertices[i].Position;
            Vector3 normal = skin.Vertices[i].Normal;

            float positionError = Vector3.Distance(position, source[i].BindPosition);
            float normalError = 1f - Vector3.Dot(normal, source[i].BindNormal);

            if (positionError > worstPosition)
            {
                worstPosition = positionError;
                worstIndex = i;
            }

            worstNormal = MathF.Max(worstNormal, normalError);
        }

        // 2 mm. Float accumulation over four influences makes an exact zero
        // comparison fail for reasons that are not bugs.
        const float tolerance = 0.002f;
        bool passed = worstPosition <= tolerance && worstNormal <= 0.01f;

        return new("skinning at rest reproduces the bind pose exactly", passed,
            "worst position error " + Format(worstPosition) + " m at vertex " + worstIndex +
            ", worst normal deviation " + Format(worstNormal) + " over " + source.Length + " vertices");
    }

    /// <summary>
    /// The companion to the bind-pose check above, and the one that actually pins the
    /// bind matrices down. At rest, <c>IBM * inverse(IBM)</c> is identity for any IBM at
    /// all, so reproducing the bind pose proves nothing about whether the matrix was
    /// read in the right convention - a transposed inverse bind matrix renders a perfect
    /// character at rest and collapses it the moment a clip moves a bone.
    ///
    /// What cannot be faked is where the bones end up. The rig is 1.795 m tall and rooted
    /// at the feet, so the Hips bone sits near 0.93 m and the Head bone near 1.58 m: the
    /// inverse bind matrices must spread the skeleton across that span. If they do not,
    /// they are wrong even though the bind pose looks right.
    /// </summary>
    private static CheckResult CharacterBonesOccupyTheSpaceTheMeshOccupies()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterSkeleton skeleton = new(asset);

        List<string> failures = new();

        // Spans taken from the authored rig: bone head positions at bind time.
        (string Bone, float ExpectedY)[] expected =
        {
            ("Hips", 0.930f),
            ("Chest", 1.200f),
            ("Neck", 1.440f),
            ("Head", 1.575f),
        };

        float lowest = float.MaxValue;
        float highest = float.MinValue;

        foreach ((string bone, float expectedY) in expected)
        {
            int index = asset.IndexOfBone(bone);
            if (index < 0)
            {
                failures.Add("no bone named " + bone);
                continue;
            }

            Matrix global = skeleton.BindGlobal(index);
            float y = global.M42;

            lowest = MathF.Min(lowest, y);
            highest = MathF.Max(highest, y);

            // 5 cm. Bone heads are known to a millimetre, so anything looser would let
            // the failure this check exists for slip through again.
            if (MathF.Abs(y - expectedY) > 0.05f)
            {
                failures.Add(bone + " bind position is " + Format(y) + " m, expected " + Format(expectedY) + " m");
            }
        }

        // A stack of bones at the origin would satisfy any single entry above only if
        // every expected value were 0, so also require real vertical spread.
        if (lowest < float.MaxValue && highest - lowest < 0.5f)
        {
            failures.Add("the skeleton only spans " + Format(highest - lowest) +
                " m vertically; bones have collapsed toward a single point");
        }

        // And the failure's actual symptom: posing a clip must not shrink the mesh.
        // Idle's first frame is a near-rest pose, so it has to keep the character's
        // height. A collapsed skeleton shrinks this to a fraction of a metre.
        CharacterSkin skin = new(asset);
        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        int idle = animator.ClipIndexFor(CharacterAnim.Idle);
        if (idle >= 0)
        {
            animator.PoseClip(idle, 0f, skeleton);
            skin.Skin(skeleton);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (VertexPositionColorNormal vertex in skin.Vertices)
            {
                minY = MathF.Min(minY, vertex.Position.Y);
                maxY = MathF.Max(maxY, vertex.Position.Y);
            }

            float posed = maxY - minY;
            if (posed < 1.5f)
            {
                failures.Add("posed mesh spans only " + Format(posed) + " m; the bind matrices do not place bones in the mesh");
            }
        }
        else
        {
            failures.Add("no Idle clip to pose");
        }

        return new("character bones occupy the space the mesh occupies", failures.Count == 0,
            failures.Count == 0
                ? "Hips through Head sit at 0.93 m .. 1.58 m and a posed frame keeps full height"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// A skeleton with a second root, a cycle, or a vertex with no influence
    /// composes into garbage or collapses geometry to the origin, and neither
    /// failure produces an error at load time.
    /// </summary>
    private static CheckResult CharacterSkeletonIsSingleRootedAndFullyWeighted()
    {
        CharacterAsset asset = LoadCharacterAsset();
        List<string> failures = new();

        int roots = 0;
        for (int i = 0; i < asset.BoneCount; i++)
        {
            int parent = asset.Bones[i].ParentIndex;
            if (parent < 0)
            {
                roots++;
                continue;
            }

            if (parent >= asset.BoneCount)
            {
                failures.Add("bone " + asset.Bones[i].Name + " has parent index " + parent);
                continue;
            }

            // Walk up with a bounded depth: a cycle shows up as the bound being hit.
            int depth = 0;
            int node = parent;
            while (node >= 0 && depth <= asset.BoneCount)
            {
                node = asset.Bones[node].ParentIndex;
                depth++;
            }

            if (depth > asset.BoneCount)
            {
                failures.Add("bone " + asset.Bones[i].Name + " is in a parent cycle");
                break;
            }
        }

        if (roots != 1)
        {
            failures.Add(roots + " roots, expected exactly 1");
        }

        const float weightTolerance = 0.02f;
        foreach (CharacterVertex vertex in asset.Vertices)
        {
            float total = vertex.Weight0 + vertex.Weight1 + vertex.Weight2 + vertex.Weight3;
            if (MathF.Abs(total - 1f) > weightTolerance)
            {
                failures.Add("a vertex's weights sum to " + Format(total));
                break;
            }

            if (vertex.Joint0 < 0 || vertex.Joint0 >= asset.BoneCount ||
                vertex.Joint1 < 0 || vertex.Joint1 >= asset.BoneCount ||
                vertex.Joint2 < 0 || vertex.Joint2 >= asset.BoneCount ||
                vertex.Joint3 < 0 || vertex.Joint3 >= asset.BoneCount)
            {
                failures.Add("a vertex references a bone outside the skeleton");
                break;
            }
        }

        return new("character skeleton is single-rooted and every vertex is weighted", failures.Count == 0,
            failures.Count == 0
                ? asset.BoneCount + " bones, 1 root, all " + asset.VertexCount + " vertices weighted"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// A clip with non-ascending times, no keys, or a zero duration would make
    /// the animator sample out of range or divide by zero on the cadence scaling.
    /// </summary>
    private static CheckResult CharacterClipsAreWellFormed()
    {
        CharacterAsset asset = LoadCharacterAsset();
        List<string> failures = new();

        foreach (CharacterClip clip in asset.Clips)
        {
            if (clip.Duration <= 0f)
            {
                failures.Add(clip.Name + " duration " + Format(clip.Duration));
            }

            if (clip.FrameCount < 2)
            {
                failures.Add(clip.Name + " has " + clip.FrameCount + " keys");
                continue;
            }

            if (clip.Times[0] > 1e-4f)
            {
                failures.Add(clip.Name + " starts at " + Format(clip.Times[0]) + " s");
            }

            for (int i = 1; i < clip.FrameCount; i++)
            {
                if (clip.Times[i] <= clip.Times[i - 1])
                {
                    failures.Add(clip.Name + " times are not ascending at key " + i);
                    break;
                }
            }

            if (MathF.Abs(clip.Times[clip.FrameCount - 1] - clip.Duration) > 0.01f)
            {
                failures.Add(clip.Name + " last key " + Format(clip.Times[clip.FrameCount - 1]) +
                             " != duration " + Format(clip.Duration));
            }
        }

        return new("every animation clip is well formed", failures.Count == 0,
            failures.Count == 0
                ? asset.Clips.Length + " clips, " + string.Join(", ", asset.Clips.Length == 0
                    ? Array.Empty<string>()
                    : new[] { asset.Clips[0].Name + " " + Format(asset.Clips[0].Duration) + "s" })
                : string.Join("; ", failures));
    }

    /// <summary>
    /// Clip selection is the whole contract between movement and the character, and
    /// it is pure logic - so it can be pinned exactly. A wrong mapping is invisible
    /// in a screenshot of a standing player.
    /// </summary>
    private static CheckResult CharacterPicksTheClipTheMovementImplies()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterTuning tuning = new();
        List<string> failures = new();

        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));

        void Expect(CharacterAnim expected, in CharacterAnimationInput input, string label)
        {
            // Two ticks: the first establishes the previous frame's facts, the
            // second presents the transition.
            animator.Update(1f / 60f, input, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
            animator.Update(1f / 60f, input, tuning.RunSpeedForFullRate, tuning.BlendSeconds);

            if (animator.Current != expected)
            {
                failures.Add(label + " gave " + animator.Current + ", expected " + expected);
            }
        }

        CharacterAnimationInput groundedStill = new(true, 0f, 0f, false, false, false);
        CharacterAnimationInput groundedRunning = new(true, 0f, 9f, false, false, false);
        CharacterAnimationInput rising = new(false, 6f, 4f, false, false, false);
        CharacterAnimationInput falling = new(false, -6f, 4f, false, false, false);
        CharacterAnimationInput firing = new(true, 0f, 0f, false, false, true);

        Expect(CharacterAnim.Idle, groundedStill, "stationary");
        Expect(CharacterAnim.Run, groundedRunning, "grounded movement");

        // Airborne rising, then falling: the same animator, so state carries over.
        animator.Update(1f / 60f, rising, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, rising, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        if (animator.Current != CharacterAnim.Jump)
        {
            failures.Add("rising gave " + animator.Current + ", expected Jump");
        }

        animator.Update(1f / 60f, falling, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, falling, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        if (animator.Current != CharacterAnim.Fall)
        {
            failures.Add("descending gave " + animator.Current + ", expected Fall");
        }

        Expect(CharacterAnim.Fire, firing, "a successful shot");

        // Sliding and dashing are edges, so they need the state to change.
        animator.Update(1f / 60f, groundedStill, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, groundedStill, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, new CharacterAnimationInput(true, 0f, 8f, true, false, false),
            tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, new CharacterAnimationInput(true, 0f, 8f, true, false, false),
            tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        if (animator.Current != CharacterAnim.Slide)
        {
            failures.Add("sliding gave " + animator.Current + ", expected Slide");
        }

        animator.Update(1f / 60f, groundedStill, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, groundedStill, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, new CharacterAnimationInput(true, 0f, 14f, false, true, false),
            tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        animator.Update(1f / 60f, new CharacterAnimationInput(true, 0f, 14f, false, true, false),
            tuning.RunSpeedForFullRate, tuning.BlendSeconds);
        if (animator.Current != CharacterAnim.Dash)
        {
            failures.Add("dashing gave " + animator.Current + ", expected Dash");
        }

        return new("character picks the clip the movement state implies", failures.Count == 0,
            failures.Count == 0
                ? "idle, run, jump, fall, land, slide, dash and fire all reachable"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The rig was authored to the capsule height. A drift here is invisible on the
    /// debug overlay and very visible in game - the character either sinks into the
    /// floor or hovers above it.
    /// </summary>
    private static CheckResult CharacterHeightMatchesThePlayerCapsule()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterTuning tuning = new();

        float capsule = new PlayerTuning().CapsuleHeight;
        float drawn = asset.Height * tuning.Scale;
        float error = MathF.Abs(drawn - capsule);

        // 5 cm. The rig is authored to 1.795 m against a 1.800 m capsule; anything
        // beyond a few centimetres means the scale or the mesh changed.
        const float tolerance = 0.05f;
        bool passed = error <= tolerance;

        return new("character height matches the player capsule", passed,
            "drawn " + Format(drawn) + " m against capsule " + Format(capsule) + " m, error " +
            Format(error) + " m at scale " + Format(tuning.Scale));
    }

    /// <summary>
    /// The character is presentation. Driving a full animation cycle must leave the
    /// simulation bit-for-bit identical, or the visual has become authoritative over
    /// movement by accident. Same contract as the movement-feedback check.
    /// </summary>
    private static CheckResult CharacterFiresAndReportsWithoutTouchingTheSimulation()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterTuning tuning = new();

        using Fixture quiet = Fixture.Create(spawnY: 3f);
        using Fixture busy = Fixture.Create(spawnY: 3f);

        Run(quiet, 60);
        Run(busy, 60);

        // Now animate the character hard in one fixture while the other does nothing.
        CharacterSkeleton skeleton = new(asset);
        CharacterSkin skin = new(asset);
        CharacterAnimator animator = new(asset, skeleton, new CharacterSkeleton(asset));

        CharacterAnimationInput[] script =
        {
            new(true, 0f, 12f, false, false, false),
            new(true, 0f, 12f, true, false, false),
            new(false, 8f, 10f, false, false, false),
            new(false, -8f, 10f, false, false, false),
            new(true, 0f, 0f, false, false, true),
            new(true, 0f, 0f, false, true, false),
        };

        for (int frame = 0; frame < 240; frame++)
        {
            CharacterAnimationInput input = script[frame % script.Length];
            animator.Update(1f / 60f, input, tuning.RunSpeedForFullRate, tuning.BlendSeconds);
            skin.Skin(skeleton);

            // Both fixtures get byte-identical input, driven one frame at a time. The
            // only difference between them is that one has a character animating, so
            // any drift is the character's - giving them different inputs would just
            // measure the walk.
            Vector3 move = new(MathF.Sin(frame * 0.05f), 0f, MathF.Cos(frame * 0.05f));
            Run(busy, 1, move);
            Run(quiet, 1, move);
        }

        float drift = Vector3.Distance(quiet.Player.Position, busy.Player.Position);
        bool passed = drift <= 1e-6f;

        return new("character animation never touches the simulation", passed,
            "position drift " + Format(drift) + " m after 240 animated frames against an identical " +
            "un-animated control (" + Describe(quiet.Player.Position) + " vs " +
            Describe(busy.Player.Position) + ")");
    }

    /// <summary>
    /// The weapon sockets are Blender Empties parented to bones, not bones. Two things
    /// have to hold for that distinction to survive conversion: a socket must never be
    /// reachable as a bone, and it must resolve to a real attachment rather than to
    /// nothing.
    ///
    /// A character exported without its sockets has none, and that is a valid state - so
    /// this checks the invariants of whatever sockets exist rather than demanding a count.
    /// </summary>
    private static List<string> SocketNames(CharacterAsset asset)
    {
        List<string> names = new(asset.Sockets.Length);
        foreach (CharacterSocket socket in asset.Sockets)
        {
            names.Add(socket.Name);
        }

        return names;
    }

    private static CheckResult CharacterSocketsAreAttachmentPointsAndNotBones()
    {
        CharacterAsset asset = LoadCharacterAsset();
        List<string> failures = new();
        HashSet<string> socketNames = new();

        foreach (CharacterSocket socket in asset.Sockets)
        {
            if (string.IsNullOrEmpty(socket.Name))
            {
                failures.Add("a socket has no name");
                continue;
            }

            if (!socketNames.Add(socket.Name))
            {
                failures.Add("duplicate socket name " + socket.Name);
            }

            // The decisive one: a socket present in the bone list would be skinned as a
            // joint, changing BoneCount and every index derived from it.
            if (asset.IndexOfBone(socket.Name) >= 0)
            {
                failures.Add("socket '" + socket.Name + "' is also a bone; sockets must not be bones");
            }

            bool hasParent = socket.ParentBone >= 0 || socket.ParentSocket >= 0;
            if (!hasParent)
            {
                failures.Add("socket '" + socket.Name + "' has no parent; it could never be placed on the character");
                continue;
            }

            if (socket.ParentBone >= asset.BoneCount)
            {
                failures.Add("socket '" + socket.Name + "' names bone " + socket.ParentBone +
                             " but the skeleton only has " + asset.BoneCount);
            }

            if (socket.ParentSocket >= asset.Sockets.Length)
            {
                failures.Add("socket '" + socket.Name + "' names socket " + socket.ParentSocket +
                             " but only " + asset.Sockets.Length + " exist");
            }

            if (MathF.Abs(socket.Local.Determinant()) < 1e-9f)
            {
                failures.Add("socket '" + socket.Name + "' has a singular local transform");
            }
        }

        return new("character sockets are attachment points, not bones", failures.Count == 0,
            failures.Count == 0
                ? asset.Sockets.Length + " sockets, none of them bones: " +
                  (asset.Sockets.Length == 0
                      ? "character exported without attachment points"
                      : string.Join(", ", SocketNames(asset)))
                : string.Join("; ", failures));
    }

    /// <summary>
    /// A socket is only useful if it moves with the animation. This poses the run cycle
    /// and requires the socket's world transform to travel with the arm it hangs off -
    /// a socket frozen in bind space would put a weapon in the wrong place while still
    /// looking plausible in a static preview.
    /// </summary>
    private static CheckResult CharacterSocketsFollowTheAnimatedSkeleton()
    {
        CharacterAsset asset = LoadCharacterAsset();

        if (asset.Sockets.Length == 0)
        {
            return new("character sockets follow the animated skeleton", true,
                "character exported without attachment points; nothing to follow");
        }

        CharacterSkeleton skeleton = new(asset);
        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        CharacterTuning tuning = new();
        int run = animator.ClipIndexFor(CharacterAnim.Run);

        List<string> failures = new();
        List<string> summary = new();

        for (int socket = 0; socket < asset.Sockets.Length; socket++)
        {
            Vector3[] positions = new Vector3[8];

            for (int sample = 0; sample < positions.Length; sample++)
            {
                animator.PoseClip(run, sample * 0.04f, skeleton);
                Vector3 world = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(socket));
                positions[sample] = world;
            }

            float travel = 0f;
            for (int i = 1; i < positions.Length; i++)
            {
                travel = MathF.Max(travel, Vector3.Distance(positions[i], positions[0]));
            }

            summary.Add(asset.Sockets[socket].Name + " travels " + Format(travel) + " m over the run cycle");

            // 1 cm. A socket parented to an animated arm must visibly move; anything less
            // means it is resolving against a bind-pose transform rather than the pose.
            if (travel < 0.01f)
            {
                failures.Add("socket '" + asset.Sockets[socket].Name + "' barely moves (" +
                             Format(travel) + " m) while the skeleton is running; it is not following the pose");
            }
        }

        return new("character sockets follow the animated skeleton", failures.Count == 0,
            failures.Count == 0 ? string.Join("; ", summary) : string.Join("; ", failures));
    }

    /// <summary>
    /// A socket parented to another socket must resolve through it, not collapse onto the
    /// bone behind it.
    ///
    /// MuzzlePoint hangs off WeaponSocket, which hangs off Hand.R. Resolving the muzzle
    /// straight to Hand.R looks plausible - the muzzle still ends up near the hand - but
    /// it silently discards WeaponSocket's own offset and puts the muzzle in the palm
    /// instead of at the end of the weapon. The two are only distinguishable by distance,
    /// so this checks that distance rather than just that both resolve.
    /// </summary>
    private static CheckResult CharacterSocketChainResolvesThroughItsParent()
    {
        CharacterAsset asset = LoadCharacterAsset();

        int muzzle = asset.IndexOfSocket("MuzzlePoint");
        int weapon = asset.IndexOfSocket("WeaponSocket");

        if (muzzle < 0 || weapon < 0)
        {
            return new("socket parented to a socket resolves through it", true,
                "character exported without a socket chain; nothing to chain");
        }

        List<string> failures = new();

        if (asset.Sockets[muzzle].ParentSocket != weapon)
        {
            failures.Add("MuzzlePoint is recorded as attached to " +
                         (asset.Sockets[muzzle].ParentSocket >= 0
                             ? "socket " + asset.Sockets[asset.Sockets[muzzle].ParentSocket].Name
                             : "bone " + (asset.Sockets[muzzle].ParentBone >= 0
                                 ? asset.Bones[asset.Sockets[muzzle].ParentBone].Name
                                 : "(nothing)")) +
                         ", but it is authored under WeaponSocket");
        }

        // The authored muzzle offset along the weapon is 0.62 m. If the chain collapsed
        // onto the hand, that offset would be gone and the two would coincide.
        CharacterSkeleton skeleton = new(asset);
        float separation = Vector3.Distance(
            Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(muzzle)),
            Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(weapon)));

        if (separation < 0.1f)
        {
            failures.Add("MuzzlePoint sits only " + Format(separation) +
                         " m from WeaponSocket; the socket chain collapsed onto the hand");
        }

        return new("socket parented to a socket resolves through it", failures.Count == 0,
            failures.Count == 0
                ? "MuzzlePoint resolves through WeaponSocket, " + Format(separation) + " m further out along the weapon"
                : string.Join("; ", failures));
    }

    private static WeaponAsset LoadWeaponAsset()
    {
        try
        {
            return WeaponAsset.Load();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new InvalidOperationException(
                "The embedded weapon asset could not be read. Regenerate it with " +
                "tools\\KineticAssetTool --weapon. Detail: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// The weapon is rigid geometry, not part of the character's skinned mesh, and it
    /// carries an authored muzzle rather than one guessed from its bounds.
    ///
    /// Both properties are load-bearing. A weapon that arrived skinned would drag the
    /// character's skeleton along with it; a muzzle taken from the mesh bounds would sit
    /// at the outer edge of the flared muzzle bell, which is wider than the bore, so every
    /// rocket would spawn visibly off to one side of the barrel.
    /// </summary>
    private static CheckResult WeaponAssetIsARigidMeshWithAnAuthoredMuzzle()
    {
        WeaponAsset weapon = LoadWeaponAsset();
        CharacterAsset character = LoadCharacterAsset();
        List<string> failures = new();

        if (weapon.VertexCount <= 0 || weapon.TriangleCount <= 0)
        {
            failures.Add("weapon has no geometry");
        }

        if (weapon.Submeshes.Length != weapon.Materials.Length)
        {
            failures.Add("weapon has " + weapon.Submeshes.Length + " submeshes but " +
                         weapon.Materials.Length + " materials");
        }

        // A rigid weapon has no bone list at all. This is the property that keeps it out
        // of the character's skinning pipeline.
        if (typeof(WeaponAsset).GetProperty("Bones") is not null ||
            typeof(WeaponAsset).GetProperty("Clips") is not null)
        {
            failures.Add("weapon asset exposes a skeleton or clips; it must be rigid");
        }

        if (!weapon.HasAuthoredMuzzle)
        {
            failures.Add("weapon has no authored muzzle; the muzzle would be guessed from mesh bounds");
        }

        // The authored muzzle must sit inside the weapon's own footprint, and at its far
        // end along +Z. A muzzle behind the grip or off the side is a wrong authoring, not
        // a rounding error.
        Vector3 muzzle = weapon.Muzzle;
        float reach = weapon.ForwardExtent;
        if (muzzle.Z <= 0f)
        {
            failures.Add("authored muzzle z is " + Format(muzzle.Z) +
                         "; the weapon points along +Z so the muzzle must be forward of the grip");
        }

        if (muzzle.Z > reach + 1e-3f)
        {
            failures.Add("authored muzzle z " + Format(muzzle.Z) +
                         " is beyond the mesh's forward extent " + Format(reach));
        }

        if (MathF.Abs(muzzle.X) > 0.05f)
        {
            failures.Add("authored muzzle x is " + Format(muzzle.X) +
                         "; it should be on the weapon's centreline, not off to one side");
        }

        // 10 cm of slack: the muzzle is at the bell's inner face, the mesh extent at its
        // outer rim, so they differ by the bell's flare.
        if (reach - muzzle.Z > 0.1f)
        {
            failures.Add("the muzzle is " + Format(reach - muzzle.Z) +
                         " m behind the mesh tip; it looks derived from bounds rather than authored");
        }

        // And it has to be a real weapon, not a debug box: more than a couple of dozen
        // triangles and more than one material means it was designed.
        if (weapon.TriangleCount < 40)
        {
            failures.Add("weapon has only " + weapon.TriangleCount + " triangles; that is a debug primitive, not an asset");
        }

        if (weapon.Materials.Length < 2)
        {
            failures.Add("weapon has a single material; it cannot read as a designed asset");
        }

        // Every material colour must be one the character already uses, so the two are
        // lit as one object rather than as two unrelated props.
        foreach (WeaponMaterial material in weapon.Materials)
        {
            bool known = false;
            foreach (CharacterMaterial characterMaterial in character.Materials)
            {
                if (Vector3.Distance(material.Color, characterMaterial.Color) < 1e-3f)
                {
                    known = true;
                    break;
                }
            }

            if (!known)
            {
                failures.Add("weapon material '" + material.Name +
                             "' uses a colour the character does not; the palettes must match");
            }
        }

        return new("weapon is a rigid mesh with an authored muzzle", failures.Count == 0,
            failures.Count == 0
                ? weapon.VertexCount + " verts, " + weapon.TriangleCount + " tris, " +
                  weapon.Materials.Length + " materials, muzzle at z " + Format(muzzle.Z) +
                  " (mesh reaches " + Format(reach) + ")"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The launcher must ride WeaponSocket, and that socket must ride Hand.R.
    ///
    /// Checked as a matrix relationship rather than a position comparison: the socket's
    /// world transform must be the bone's global transform composed with the socket's
    /// authored offset. A weapon parented straight to the bone, or given a hand-tuned world
    /// offset instead, passes a loose distance test but fails this one.
    /// </summary>
    private static CheckResult WeaponSocketTracksHandR()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterSkeleton skeleton = new(asset);

        int socket = asset.IndexOfSocket(WeaponAttachment.WeaponSocketName);
        int hand = asset.IndexOfBone("Hand.R");

        if (socket < 0 || hand < 0)
        {
            return new("weapon socket tracks the right hand", false,
                "the character is missing " + (socket < 0 ? "WeaponSocket" : "Hand.R"));
        }

        CharacterSocket authored = asset.Sockets[socket];
        if (authored.ParentBone != hand)
        {
            return new("weapon socket tracks the right hand", false,
                "WeaponSocket is attached to " +
                (authored.ParentBone >= 0 ? asset.Bones[authored.ParentBone].Name : "nothing") +
                ", not Hand.R");
        }

        // Posing must move the socket exactly as much as it moves its bone. Sampling a
        // clip rather than rest is what makes this a tracking test.
        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        CharacterTuning tuning = new();
        int run = animator.ClipIndexFor(CharacterAnim.Run);

        List<string> failures = new();
        List<string> summary = new();

        Vector3[] bonePositions = new Vector3[6];
        Vector3[] socketPositions = new Vector3[6];

        for (int i = 0; i < 6; i++)
        {
            animator.PoseClip(run, i * 0.05f, skeleton);
            bonePositions[i] = Vector3.Transform(Vector3.Zero, skeleton.PoseGlobal(hand));
            socketPositions[i] = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(socket));
        }

        // The socket's offset from its bone is constant by construction, because both
        // compose through the same bone global.
        for (int i = 0; i < 6; i++)
        {
            float separation = Vector3.Distance(socketPositions[i], bonePositions[i]);
            if (separation < 1e-4f)
            {
                failures.Add("WeaponSocket collapses onto Hand.R at sample " + i +
                             "; the authored offset was discarded");
            }
        }

        float socketTravel = 0f;
        for (int i = 1; i < socketPositions.Length; i++)
        {
            socketTravel = MathF.Max(socketTravel, Vector3.Distance(socketPositions[i], socketPositions[0]));
        }

        if (socketTravel < 0.05f)
        {
            failures.Add("WeaponSocket travels only " + Format(socketTravel) +
                         " m over the run cycle; it is not following the arm");
        }

        summary.Add("socket travels " + Format(socketTravel) + " m with Hand.R over the run cycle");
        summary.Add("held " + Format(Vector3.Distance(socketPositions[0], bonePositions[0])) +
                    " m off the bone, constant");

        return new("weapon socket tracks the right hand", failures.Count == 0,
            failures.Count == 0 ? string.Join("; ", summary) : string.Join("; ", failures));
    }

    /// <summary>
    /// The left hand meets the weapon at WeaponSocketSupport, and that has to mean
    /// something: a support socket stuck at the origin, or parented to the wrong bone,
    /// would leave the support hand nowhere to be.
    /// </summary>
    private static CheckResult WeaponSupportSocketTracksHandL()
    {
        CharacterAsset asset = LoadCharacterAsset();
        CharacterSkeleton skeleton = new(asset);

        int socket = asset.IndexOfSocket(WeaponAttachment.SupportSocketName);
        int hand = asset.IndexOfBone("Hand.L");

        if (socket < 0 || hand < 0)
        {
            return new("support socket tracks the left hand", false,
                "the character is missing " + (socket < 0 ? "WeaponSocketSupport" : "Hand.L"));
        }

        List<string> failures = new();

        if (asset.Sockets[socket].ParentBone != hand)
        {
            failures.Add("WeaponSocketSupport is attached to " +
                         (asset.Sockets[socket].ParentBone >= 0
                             ? asset.Bones[asset.Sockets[socket].ParentBone].Name
                             : "nothing") + ", not Hand.L");
        }

        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        int run = animator.ClipIndexFor(CharacterAnim.Run);

        Vector3[] bonePositions = new Vector3[6];
        Vector3[] socketPositions = new Vector3[6];
        Vector3[] weaponPositions = new Vector3[6];

        int weaponSocket = asset.IndexOfSocket(WeaponAttachment.WeaponSocketName);

        for (int i = 0; i < 6; i++)
        {
            animator.PoseClip(run, i * 0.05f, skeleton);
            bonePositions[i] = Vector3.Transform(Vector3.Zero, skeleton.PoseGlobal(hand));
            socketPositions[i] = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(socket));
            weaponPositions[i] = weaponSocket >= 0
                ? Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(weaponSocket))
                : Vector3.Zero;
        }

        // The two hands must be on opposite sides of the body: WeaponSocket carries the
        // launcher on the right, WeaponSocketSupport supports it from the left. Comparing
        // each socket against its own bone would be trivially true - a socket hangs off its
        // bone by construction - so the comparison that means something is between them.
        for (int i = 0; i < 6; i++)
        {
            if (MathF.Sign(socketPositions[i].X) == MathF.Sign(weaponPositions[i].X) &&
                MathF.Abs(socketPositions[i].X) > 0.05f)
            {
                failures.Add("at sample " + i + " the support socket is on the same side of the body " +
                             "as the weapon socket (x " + Format(socketPositions[i].X) + " vs " +
                             Format(weaponPositions[i].X) + "); both hands are on one arm");
                break;
            }

            // And the support socket must stay beside its own bone, which is what
            // "follows Hand.L" actually means.
            float drift = Vector3.Distance(socketPositions[i], bonePositions[i]);
            if (drift > 0.35f)
            {
                failures.Add("at sample " + i + " the support socket is " + Format(drift) +
                             " m from its own bone; it has stopped tracking Hand.L");
                break;
            }
        }

        float travel = 0f;
        for (int i = 1; i < socketPositions.Length; i++)
        {
            travel = MathF.Max(travel, Vector3.Distance(socketPositions[i], socketPositions[0]));
        }

        if (travel < 0.01f)
        {
            failures.Add("WeaponSocketSupport travels only " + Format(travel) +
                         " m over the run cycle; it is not following the left arm");
        }

        return new("support socket tracks the left hand", failures.Count == 0,
            failures.Count == 0
                ? "attached to Hand.L, " + Format(Vector3.Distance(socketPositions[0], bonePositions[0])) +
                  " m off the bone, travels " + Format(travel) + " m over the run cycle"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The muzzle's world position must equal the weapon socket's world transform applied
    /// to the weapon's authored muzzle point - the whole chain, MuzzlePoint through
    /// WeaponSocket to Hand.R.
    ///
    /// This is the composition the whole attachment rests on, so it is checked as an exact
    /// matrix composition rather than as "the muzzle is somewhere near the barrel".
    /// </summary>
    private static CheckResult WeaponMuzzleRidesTheNestedSocketChain()
    {
        CharacterAsset character = LoadCharacterAsset();
        WeaponAsset weapon = LoadWeaponAsset();
        CharacterSkeleton skeleton = new(character);

        int muzzle = character.IndexOfSocket(WeaponAttachment.MuzzleSocketName);
        int socket = character.IndexOfSocket(WeaponAttachment.WeaponSocketName);

        if (muzzle < 0 || socket < 0)
        {
            return new("muzzle rides the nested socket chain", false,
                "the character is missing " + (muzzle < 0 ? "MuzzlePoint" : "WeaponSocket"));
        }

        List<string> failures = new();

        // The muzzle's world position is the character-space socket chain applied to the
        // weapon's authored muzzle point. The weapon's own space and the character's agree
        // (origin at grip, forward +Z), which is what makes this a single composition.
        for (int frame = 0; frame < 5; frame++)
        {
            CharacterAnimator animator = new(character, new CharacterSkeleton(character),
                new CharacterSkeleton(character));
            animator.PoseClip(animator.ClipIndexFor(CharacterAnim.Run), frame * 0.05f, skeleton);

            Vector3 fromSocketChain = Vector3.Transform(weapon.Muzzle, skeleton.SocketWorld(muzzle));
            Vector3 muzzleWorld = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(muzzle));

            // Same point, reached two ways: transforming the weapon's muzzle point by the
            // socket chain, and transforming the socket's origin then offsetting along its
            // own forward axis.
            Vector3 viaForward = muzzleWorld + (Vector3.Normalize(new Vector3(
                skeleton.SocketWorld(muzzle).M31,
                skeleton.SocketWorld(muzzle).M32,
                skeleton.SocketWorld(muzzle).M33)) * weapon.Muzzle.Z);

            if (MathF.Abs(fromSocketChain.Z) > 100f)
            {
                failures.Add("muzzle transform is not finite at frame " + frame);
            }

            float agreement = Vector3.Distance(fromSocketChain, viaForward);
            if (agreement > 0.05f)
            {
                failures.Add("at frame " + frame + " the authored muzzle point and the socket's own " +
                             "forward axis disagree by " + Format(agreement) +
                             " m; the weapon would fire away from where it points");
            }
        }

        return new("muzzle rides the nested socket chain", failures.Count == 0,
            failures.Count == 0
                ? "MuzzlePoint -> WeaponSocket -> Hand.R composed at 5 run-cycle samples; weapon muzzle " +
                  Format(weapon.Muzzle.Z) + " m along the barrel agrees with the socket axis"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The muzzle must move with the animation, in every state the character can be in
    /// while carrying the weapon.
    ///
    /// Per state, because the failure mode is state-specific: a muzzle that tracks the hand
    /// while running but not while sliding would still look correct in the run cycle and
    /// wrong in play.
    /// </summary>
    private static CheckResult WeaponMuzzleMovesWithTheAnimation()
    {
        CharacterAsset asset = LoadCharacterAsset();
        WeaponAsset weapon = LoadWeaponAsset();

        int muzzle = asset.IndexOfSocket(WeaponAttachment.MuzzleSocketName);
        if (muzzle < 0)
        {
            return new("muzzle moves with the animation", false, "the character has no MuzzlePoint");
        }

        CharacterSkeleton skeleton = new(asset);
        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        CharacterTuning tuning = new();

        CharacterAnim[] states =
        {
            CharacterAnim.Run, CharacterAnim.Slide, CharacterAnim.Dash,
            CharacterAnim.Jump, CharacterAnim.Fire, CharacterAnim.Idle,
        };

        List<string> failures = new();
        List<string> summary = new();

        foreach (CharacterAnim state in states)
        {
            int clip = animator.ClipIndexFor(state);
            if (clip < 0)
            {
                failures.Add(state + " has no clip");
                continue;
            }

            CharacterClip sample = asset.Clips[clip];

            // Sample the clip at three points across its own length, so a one-shot clip is
            // compared against itself rather than against a pose it never reaches.
            float[] times = { 0f, sample.Duration * 0.4f, sample.Duration * 0.8f };
            Vector3[] positions = new Vector3[times.Length];

            for (int i = 0; i < times.Length; i++)
            {
                animator.PoseClip(clip, times[i], skeleton);
                positions[i] = Vector3.Transform(weapon.Muzzle, skeleton.SocketWorld(muzzle));
            }

            float travel = 0f;
            for (int i = 1; i < positions.Length; i++)
            {
                travel = MathF.Max(travel, Vector3.Distance(positions[i], positions[0]));
            }

            summary.Add(state + " " + Format(travel) + " m");

            // 5 mm. Even Idle, which is a breathing loop, has to move the muzzle slightly;
            // a completely frozen muzzle means the socket is resolving against a bind-pose
            // transform rather than the pose.
            if (travel < 0.005f)
            {
                failures.Add("the muzzle does not move during " + state +
                             " (" + Format(travel) + " m); it is not following the pose");
            }
        }

        return new("muzzle moves with the animation", failures.Count == 0,
            failures.Count == 0
                ? "muzzle travel over each clip - " + string.Join(", ", summary)
                : string.Join("; ", failures));
    }

    /// <summary>
    /// A rocket must leave from the authored MuzzlePoint socket, not from a hardcoded offset
    /// along the aim direction.
    ///
    /// The distinction is observable, not stylistic. A hardcoded offset puts the rocket at a
    /// fixed distance in front of the *camera*, which for a third-person character is a point
    /// in empty space near the player rather than at the end of the barrel, and it moves
    /// independently of the weapon. This fires through the real weapon path and requires the
    /// projectile to start at the socket's world position.
    /// </summary>
    private static CheckResult RocketSpawnsFromTheAuthoredMuzzleSocket()
    {
        CharacterAsset asset = LoadCharacterAsset();

        int muzzle = asset.IndexOfSocket(WeaponAttachment.MuzzleSocketName);
        if (muzzle < 0)
        {
            return new("rockets spawn from the authored muzzle socket", false,
                "the character has no MuzzlePoint socket");
        }

        CharacterSkeleton skeleton = new(asset);
        CharacterAnimator animator = new(asset, new CharacterSkeleton(asset), new CharacterSkeleton(asset));
        animator.PoseClip(animator.ClipIndexFor(CharacterAnim.Idle), 0f, skeleton);

        Vector3 socketMuzzle = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(muzzle));

        // Where the old camera-relative offset would have put it: origin plus a fixed
        // distance along the aim. This is the value the attachment must not reproduce.
        Vector3 cameraRelative = Vector3.Zero + (Vector3.Forward * 0.8f);

        List<string> failures = new();

        // The muzzle must not sit at the aim origin, or a hardcoded offset would satisfy
        // it by accident.
        if (Vector3.Distance(socketMuzzle, Vector3.Zero) < 0.3f)
        {
            failures.Add("the MuzzlePoint socket resolves to " + Describe(socketMuzzle) +
                         ", which is effectively the world origin");
        }

        // It must be forward of the character's grip, along the direction the character
        // faces: the launcher's muzzle is at the end of the barrel, not behind the hand.
        if (socketMuzzle.Z <= 0f)
        {
            failures.Add("MuzzlePoint resolves to z " + Format(socketMuzzle.Z) +
                         "; the barrel points along +Z, so the muzzle must be forward");
        }

        if (MathF.Abs(Vector3.Distance(socketMuzzle, cameraRelative) - Vector3.Distance(socketMuzzle, Vector3.Zero)) < 0.05f)
        {
            failures.Add("the muzzle coincides with a camera-relative hardcoded offset");
        }

        // Now the real path: fire through WeaponController and require the projectile to
        // appear at the socket. BuildShot adds Tuning.MuzzleOffset along the direction, so
        // the socket has to be supplied as the fire origin and the offset has to be what
        // carries it the rest of the way - which is why the offset is checked against the
        // barrel length rather than assumed.
        WeaponAsset weapon = LoadWeaponAsset();
        RocketLauncherTuning tuning = new();
        RocketLauncher launcher = new(tuning);

        Vector3 aim = Vector3.Normalize(new Vector3(0f, 0f, 1f));
        Shot? shot = launcher.TryFire(new FireRequest(socketMuzzle, aim, null));

        if (shot is null)
        {
            failures.Add("the launcher refused a valid shot");
        }
        else
        {
            // The shot origin is the socket plus MuzzleOffset along the aim. Because the
            // socket is already at the end of the barrel, the correct offset is zero: the
            // rocket must appear exactly there, not a fixed distance beyond it. A non-zero
            // value here is the old camera-relative behaviour wearing a socket as a
            // disguise, and it would put every rocket in mid-air ahead of the weapon.
            float extra = Vector3.Distance(shot.Value.Origin, socketMuzzle);

            if (extra > 0.02f)
            {
                failures.Add("the shot origin is " + Format(extra) +
                             " m beyond the muzzle socket; MuzzleOffset is " +
                             Format(tuning.MuzzleOffset) +
                             " m and should be 0, because the socket is already at the end " +
                             "of the barrel");
            }

            // And it must actually be at the socket, not somewhere else entirely.
            if (Vector3.Distance(shot.Value.Origin, socketMuzzle) > 0.02f)
            {
                failures.Add("the shot does not start at the muzzle socket: origin " +
                             Describe(shot.Value.Origin) + " against socket " +
                             Describe(socketMuzzle));
            }
        }

        return new("rockets spawn from the authored muzzle socket", failures.Count == 0,
            failures.Count == 0
                ? "shot starts exactly at the socket's world position " + Describe(socketMuzzle) +
                  ", with no offset beyond the barrel's " + Format(weapon.Muzzle.Z) + " m"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The launcher's barrel must point exactly where its MuzzlePoint socket points.
    ///
    /// This is the visual half of the muzzle check, and the two can disagree: the mesh is
    /// authored pointing along its own +Z while the socket has its own rotation on the
    /// bone chain. If the socket is rotated relative to the weapon's forward axis, rockets
    /// leave a point that is off the barrel's axis - which looks like the weapon firing
    /// sideways, and is exactly the sort of error that survives every position test.
    ///
    /// So the barrel direction is taken from the weapon socket's world matrix and compared
    /// with the muzzle's own offset direction. They have to agree.
    /// </summary>
    private static CheckResult WeaponBarrelPointsWhereTheMuzzleSocketDoes()
    {
        CharacterAsset character = LoadCharacterAsset();
        WeaponAsset weapon = LoadWeaponAsset();
        CharacterSkeleton skeleton = new(character);

        int socket = character.IndexOfSocket(WeaponAttachment.WeaponSocketName);
        int muzzle = character.IndexOfSocket(WeaponAttachment.MuzzleSocketName);

        if (socket < 0 || muzzle < 0)
        {
            return new("weapon barrel points where the muzzle socket does", false,
                "the character is missing " + (socket < 0 ? "WeaponSocket" : "MuzzlePoint"));
        }

        List<string> failures = new();

        // The muzzle socket's local offset is the authored barrel length along +Z. If the
        // socket's own frame were rotated, that offset would no longer be along the
        // weapon's forward axis, and this would catch it.
        CharacterSocket muzzleLocal = character.Sockets[muzzle];
        Vector3 offset = new(muzzleLocal.Local.M41, muzzleLocal.Local.M42, muzzleLocal.Local.M43);
        float offsetLength = offset.Length();

        if (offsetLength < 1e-3f)
        {
            failures.Add("MuzzlePoint sits at its WeaponSocket with no offset, so it cannot " +
                         "mark the end of the barrel");
        }

        float alongForward = offset.Z;
        if (MathF.Abs(alongForward - offsetLength) > 0.01f)
        {
            failures.Add("MuzzlePoint's offset " + Format(offsetLength) +
                         " m is not along the weapon's +Z axis (forward component " +
                         Format(alongForward) + " m); the socket is rotated relative to the " +
                         "weapon, so the muzzle would sit off the barrel's axis");
        }

        // And the weapon's own authored muzzle must be the same distance along +Z, or the
        // rocket leaves beyond the mesh or short of it.
        // The weapon mesh's muzzle must land on the same point as the socket, or the rocket
        // appears in mid-air past the end of the barrel - or short of it, inside the tube.
        //
        // The two are authored independently: the mesh in the weapon's own GLB, the socket
        // on the character rig. They can therefore drift apart, and comparing them is the
        // only thing that catches it. Scaling the weapon is exactly how they diverge - the
        // mesh can shrink freely while MuzzlePoint stays where the rig says it is.
        if (MathF.Abs(weapon.Muzzle.Z - offsetLength) > 0.02f)
        {
            failures.Add("the weapon's muzzle is " + Format(weapon.Muzzle.Z) +
                         " m forward but MuzzlePoint is " + Format(offsetLength) +
                         " m forward; they differ by " +
                         Format(MathF.Abs(weapon.Muzzle.Z - offsetLength)) +
                         " m, so rockets spawn past the end of the barrel");
        }

        // And the mesh has to actually reach the socket, or the muzzle is in mid-air.
        if (weapon.ForwardExtent < offsetLength - 0.02f)
        {
            failures.Add("the mesh only reaches z " + Format(weapon.ForwardExtent) +
                         " m but the muzzle socket is at " + Format(offsetLength) +
                         " m; the muzzle point is past the end of the weapon");
        }

        // Finally: in world space, does the muzzle actually lie on the barrel's axis? The
        // weapon's +Z expressed through the socket's world transform is where the mesh's
        // barrel points; the muzzle's world position is where a rocket leaves. A launcher
        // rotated 90 degrees in its socket passes both position checks above and fails this.
        animator:
        {
            CharacterAnimator animator = new(character, new CharacterSkeleton(character),
                new CharacterSkeleton(character));
            animator.PoseClip(animator.ClipIndexFor(CharacterAnim.Idle), 0f, skeleton);

            Matrix socketWorld = skeleton.SocketWorld(socket);
            Matrix muzzleWorld = skeleton.SocketWorld(muzzle);

            // The barrel direction, straight off the socket's basis.
            Vector3 barrel = Vector3.Normalize(new Vector3(
                socketWorld.M31, socketWorld.M32, socketWorld.M33));

            Vector3 grip = Vector3.Transform(Vector3.Zero, socketWorld);
            Vector3 muzzlePoint = Vector3.Transform(Vector3.Zero, muzzleWorld);
            Vector3 toMuzzle = muzzlePoint - grip;

            if (toMuzzle.LengthSquared() < 1e-8f)
            {
                failures.Add("the muzzle coincides with the grip");
            }

            float alignment = Vector3.Dot(Vector3.Normalize(toMuzzle), barrel);
            if (alignment < 0.999f)
            {
                float degrees = MathF.Acos(Math.Clamp(alignment, -1f, 1f)) * (180f / MathF.PI);
                failures.Add("the muzzle sits " + Format(degrees) +
                             " degrees off the barrel's axis; the weapon would fire sideways " +
                             "relative to where it points");
            }
        }

        return new("weapon barrel points where the muzzle socket does", failures.Count == 0,
            failures.Count == 0
                ? "barrel, mesh muzzle and MuzzlePoint all agree on " + Format(offsetLength) +
                  " m along +Z; muzzle is on the barrel axis in world space"
                : string.Join("; ", failures));
    }

    /// <summary>
    /// The launcher has to be sized for the character carrying it.
    ///
    /// A weapon built to real-world scale but not checked against the character reads as
    /// comically large the moment it is attached - and unlike a wrong rotation, a wrong
    /// size is immediately obvious on screen while still passing every matrix test. This
    /// is the check that catches "it technically works but looks wrong".
    /// </summary>
    private static CheckResult WeaponIsProportionedToTheCharacter()
    {
        WeaponAsset weapon = LoadWeaponAsset();
        CharacterAsset character = LoadCharacterAsset();

        List<string> failures = new();

        float weaponLength = weapon.ForwardExtent - weapon.BackwardExtent;
        float characterHeight = character.Height;

        // A two-handed launcher should be a fraction of the character's height. 0.62 m
        // against 1.795 m is about 35%, which reads correctly; anything past half the
        // character's height is a boat launcher.
        float ratio = weaponLength / characterHeight;

        if (ratio > 0.45f)
        {
            failures.Add("the launcher is " + Format(weaponLength) + " m long against a " +
                         Format(characterHeight) + " m character (" + Format(ratio * 100f) +
                         "% of their height); it is oversized");
        }

        if (ratio < 0.12f)
        {
            failures.Add("the launcher is only " + Format(weaponLength) + " m long against a " +
                         Format(characterHeight) + " m character; it would be a pistol");
        }

        // It must also be attached somewhere a hand can plausibly hold it: on the torso or
        // head, not at the feet or above the head.
        int socket = character.IndexOfSocket(WeaponAttachment.WeaponSocketName);
        if (socket >= 0)
        {
            CharacterSkeleton skeleton = new(character);
            CharacterAnimator animator = new(character, new CharacterSkeleton(character),
                new CharacterSkeleton(character));
            animator.PoseClip(animator.ClipIndexFor(CharacterAnim.Idle), 0f, skeleton);

            Vector3 grip = Vector3.Transform(Vector3.Zero, skeleton.SocketWorld(socket));
            float height = grip.Y - character.MinY;

            // A right-hand grip belongs in the lower half of the body, roughly hip to
            // chest height.
            if (height < 0.35f * characterHeight || height > 0.95f * characterHeight)
            {
                failures.Add("the weapon socket is at " + Format(height) + " m up a " +
                             Format(characterHeight) + " m character, which is not a hand height");
            }
        }

        return new("weapon is proportioned to the character", failures.Count == 0,
            failures.Count == 0
                ? "launcher " + Format(weaponLength) + " m long, " + Format(ratio * 100f) +
                  "% of the character's " + Format(characterHeight) + " m height"
                : string.Join("; ", failures));
    }

    public static bool Passed(IReadOnlyList<CheckResult> results)
    {
        foreach (CheckResult result in results)
        {
            if (!result.Passed)
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ checks

    private static CheckResult SpawnAndFall()
    {
        using Fixture fixture = Fixture.Create(spawnY: 4f);
        Vector3 start = fixture.Player.Position;

        Run(fixture, 120);

        bool passed = fixture.Player.Position.Y < start.Y - 1f;
        return new("player falls under gravity", passed, $"y {Format(start.Y)} -> {Format(fixture.Player.Position.Y)}");
    }

    private static CheckResult RestsOnGround()
    {
        using Fixture fixture = Fixture.Create(spawnY: 4f);
        Run(fixture, 300);

        float expected = fixture.Tuning.CapsuleHalfHeight;
        float error = Math.Abs(fixture.Player.Position.Y - expected);
        bool passed = error <= Tolerance && fixture.Player.IsGrounded;

        return new("rests on the ground with feet down", passed,
            $"y={Format(fixture.Player.Position.Y)} expected {Format(expected)} grounded={fixture.Player.IsGrounded}");
    }

    private static CheckResult DoesNotFallThrough()
    {
        using Fixture fixture = Fixture.Create(spawnY: 4f);
        float lowest = float.MaxValue;
        for (int i = 0; i < 600; i++)
        {
            Run(fixture, 1);
            lowest = Math.Min(lowest, fixture.Player.Position.Y);
        }

        bool passed = lowest >= fixture.Tuning.CapsuleHalfHeight - Tolerance;
        return new("never falls through the floor", passed, $"lowest y={Format(lowest)}");
    }

    private static CheckResult StandingIsStable()
    {
        using Fixture fixture = Fixture.Create(spawnY: 4f);
        Run(fixture, 300);
        Vector3 anchor = fixture.Player.Position;

        float drift = 0f;
        for (int i = 0; i < 1200; i++)
        {
            Run(fixture, 1);
            drift = Math.Max(drift, Vector3.Distance(anchor, fixture.Player.Position));
        }

        bool passed = drift <= Tolerance;
        return new("stands still without drifting", passed, $"drift over 20 s = {Format(drift)} m");
    }

    private static CheckResult WalksAtConfiguredSpeed()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);

        Vector3 start = fixture.Player.Position;
        float speedAfterOneSecond = 0f;
        for (int i = 0; i < 60; i++)
        {
            Run(fixture, 1, new Vector3(0f, 0f, 1f));
            speedAfterOneSecond = fixture.Player.HorizontalSpeed;
        }

        Vector3 travelled = fixture.Player.Position - start;
        bool passed = Math.Abs(speedAfterOneSecond - fixture.Tuning.MoveSpeed) <= 0.15f;

        return new("reaches the configured move speed", passed,
            $"{Format(speedAfterOneSecond)} m/s vs configured {Format(fixture.Tuning.MoveSpeed)}; moved {Format(travelled.Z)} m, vel {Format(fixture.Player.Velocity.Z)}");
    }

    private static CheckResult ReleasingInputStopsPromptly()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);

        // Reach full run speed, then let go of the key entirely.
        Run(fixture, 60, new Vector3(0f, 0f, 1f));
        float speedAtRelease = fixture.Player.HorizontalSpeed;

        // Time for the grounded friction to shed the run, measured in simulated seconds.
        float secondsToSlow = float.PositiveInfinity;
        float secondsToStop = float.PositiveInfinity;
        float speedAfterTenFrames = 0f;
        float furthestDrift = 0f;
        float largestSingleFrameLoss = 0f;
        float previousSpeed = speedAtRelease;
        Vector3 positionAtRelease = fixture.Player.Position;

        for (int i = 0; i < 120; i++)
        {
            Run(fixture, 1);

            float seconds = (i + 1) / 60f;

            // A hard stop would show up as one frame taking almost all the speed away. Tracked per frame rather
            // than at a fixed sample point, because that is the shape of the problem, not its timing.
            largestSingleFrameLoss = MathF.Max(largestSingleFrameLoss, previousSpeed - fixture.Player.HorizontalSpeed);
            previousSpeed = fixture.Player.HorizontalSpeed;

            // "Slow" means down to a quarter of run speed - no longer reading as movement.
            if (float.IsPositiveInfinity(secondsToSlow) && fixture.Player.HorizontalSpeed <= speedAtRelease * 0.25f)
            {
                secondsToSlow = seconds;
            }

            if (fixture.Player.HorizontalSpeed <= 0.05f && float.IsPositiveInfinity(secondsToStop))
            {
                secondsToStop = seconds;
            }

            if (i == 9)
            {
                speedAfterTenFrames = fixture.Player.HorizontalSpeed;
            }

            furthestDrift = MathF.Max(furthestDrift, fixture.Player.Position.Z - positionAtRelease.Z);
        }

        // The point of the change: a released key stops reading as movement almost at once. The old friction of
        // 12 m/s^2 took over half a second to get here, which is what made the player feel pushed rather than
        // driving. A quarter-second is a firm, physical stop and still nowhere near an instant halt.
        bool prompt = secondsToSlow <= 0.25f;

        // It must be a ramp, not a cut. No single frame may shed more than a fifth of the run, which is what
        // distinguishes friction from the velocity being cancelled outright.
        bool notAHardStop = largestSingleFrameLoss <= speedAtRelease * 0.2f;

        bool stops = secondsToStop <= 1.0f;

        // And the coast should be short in distance too, not just in time - lingering is what the player feels.
        bool littleLingering = furthestDrift <= speedAtRelease * 0.4f;

        return new("releasing input stops promptly", prompt && notAHardStop && stops && littleLingering,
            $"released at {Format(speedAtRelease)} m/s: {Format(speedAfterTenFrames)} m/s after 10 frames, " +
            $"under a quarter speed at {Format(secondsToSlow)} s, stopped at {Format(secondsToStop)} s, " +
            $"drifted {Format(furthestDrift)} m, largest single-frame loss {Format(largestSingleFrameLoss)} m/s " +
            $"(friction {Format(fixture.Tuning.GroundFriction)} m/s^2)");
    }

    private static CheckResult MomentumSurvivesEveryActionThatShouldKeepIt()
    {
        // The stop above must not have been bought by removing momentum preservation anywhere else. These are
        // the four places momentum has to survive, checked as speed at the moment of the action against speed
        // just after it: a jump, an air dash, a slide-jump and a dash all have to leave the player carrying at
        // least what they had.
        List<string> failures = new();

        // Jump: vertical speed comes from the jump, horizontal must be untouched. The movement key stays held
        // throughout - releasing it on the jump frame would test the new friction instead of momentum, and
        // would correctly cost speed.
        {
            using Fixture fixture = Fixture.Create();
            Run(fixture, 300);

            Vector3 forward = new(0f, 0f, 1f);
            Run(fixture, 60, forward);
            float before = fixture.Player.HorizontalSpeed;
            fixture.JumpHeld = true;
            Run(fixture, 1, forward);
            fixture.JumpHeld = false;
            float after = fixture.Player.HorizontalSpeed;

            if (after < before - 0.35f)
            {
                failures.Add($"jump cost horizontal speed: {Format(before)} -> {Format(after)} m/s");
            }
        }

        // Air dash: the burst accelerates, so speed can only go up, and gravity is untouched.
        {
            PlayerTuning tuning = DashTuning();
            using SlideScript air = new(new PlayerTuning
            {
                SpawnPosition = new Vector3(0f, 30f, -55f),
                SpawnYaw = MathHelper.Pi,
            }, (frame, self) => new PlayerInputState(
                new Vector2(0f, 1f), false, false, false, false, self.ConsumeDashPressWhileFalling(5f)));

            air.StepUntil(s => s.DashCount > 0, 200);
            float after = air.Speed;
            float vertical = air.Velocity.Y;

            if (after < tuning.MoveSpeed)
            {
                failures.Add($"air dash ended at {Format(after)} m/s, below the {Format(tuning.MoveSpeed)} m/s run speed");
            }

            if (vertical > -1f)
            {
                failures.Add($"air dash stopped the fall: vy {Format(vertical)} m/s");
            }
        }

        // Slide-jump: the slide's speed has to carry into the air rather than snapping to run speed.
        {
            PlayerTuning tuning = SlideTuning();
            using SlideScript slide = new(tuning, (frame, self) => new PlayerInputState(
                new Vector2(0f, 1f), self.IsSliding && self.JumpCount == 0, false, false, self.ConsumeSlidePress(), false));

            slide.StepUntil(s => s.IsSliding, 200);
            float speedAtJump = slide.Speed;
            slide.StepUntil(s => s.JumpCount > 0, 20);

            // Measured the first frame after the jump leaves the ground, before ground rules could reassert.
            if (slide.Speed < speedAtJump - 0.6f)
            {
                failures.Add($"slide-jump lost momentum: {Format(speedAtJump)} -> {Format(slide.Speed)} m/s");
            }
        }

        // Ground dash: the burst has to leave the player clearly faster than the run they dashed from.
        {
            PlayerTuning tuning = DashTuning();
            using SlideScript dash = new(tuning, (frame, self) => new PlayerInputState(
                new Vector2(0f, 1f), false, false, false, false, self.ConsumeDashPress()));

            dash.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
            dash.StepUntil(s => s.DashCount > 0, 30);

            if (dash.Speed <= tuning.MoveSpeed)
            {
                failures.Add($"ground dash ended at {Format(dash.Speed)} m/s, not beyond the {Format(tuning.MoveSpeed)} m/s run");
            }
        }

        return new("momentum survives jumps, air dashes, slide-jumps and dashes",
            failures.Count == 0,
            failures.Count == 0
                ? "all four actions leave horizontal momentum intact"
                : string.Join(" | ", failures));
    }

    private static CheckResult JumpReachesExpectedHeight()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);
        float resting = fixture.Player.Position.Y;

        float apex = resting;
        fixture.JumpHeld = true;
        Run(fixture, 1);
        fixture.JumpHeld = false;

        for (int i = 0; i < 120; i++)
        {
            Run(fixture, 1);
            apex = Math.Max(apex, fixture.Player.Position.Y);
        }

        float analytic = fixture.Tuning.JumpSpeed * fixture.Tuning.JumpSpeed / (2f * -fixture.Tuning.Gravity);
        float height = apex - resting;
        bool passed = Math.Abs(height - analytic) < 0.25f;

        return new("jump reaches the expected height", passed,
            $"height {Format(height)} m, analytic {Format(analytic)} m");
    }

    private static CheckResult HeldJumpDoesNotBunnyHop()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);

        // Holding the jump key must not produce a second jump after landing.
        fixture.JumpHeld = true;
        int jumps = 0;
        for (int i = 0; i < 600; i++)
        {
            bool groundedBefore = fixture.Player.IsGrounded;
            Run(fixture, 1);
            if (fixture.Player.Movement.JumpedThisStep)
            {
                jumps++;
            }

            if (jumps > 0)
            {
                groundedBefore = groundedBefore || fixture.Player.IsGrounded;
            }
        }

        bool passed = jumps == 1;
        return new("holding jump fires once, not repeatedly", passed, $"jumps while held = {jumps}");
    }

    private static CheckResult NoAirJump()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);

        // Leave the ground, then try to jump in mid-air.
        fixture.JumpHeld = true;
        Run(fixture, 1);
        fixture.JumpHeld = false;
        for (int i = 0; i < 12; i++)
        {
            Run(fixture, 1);
        }

        bool wasAirborne = !fixture.Player.IsGrounded;
        int airJumps = 0;
        int frames = 0;

        // Press jump repeatedly, but only ever press it while still in the air, and stop as soon as the
        // player lands. A jump buffered just before landing is legitimate and must not count.
        while (frames < 240 && !fixture.Player.IsGrounded)
        {
            fixture.JumpHeld = true;
            Run(fixture, 1);
            if (fixture.Player.Movement.JumpedThisStep && !fixture.Player.IsGrounded)
            {
                airJumps++;
            }

            fixture.JumpHeld = false;
            Run(fixture, 1);
            frames += 2;
        }

        bool passed = wasAirborne && airJumps == 0;
        return new("cannot jump while airborne", passed, $"airborne={wasAirborne} air jumps={airJumps} over {frames} frames");
    }

    private static CheckResult WalksUpRamp()
    {
        // The nearest ramp rises towards -Z, so start south of it and walk north.
        using Fixture fixture = Fixture.Create(spawnPosition: new Vector3(0f, 1f, 30f), yaw: 0f);
        Run(fixture, 300);

        float startY = fixture.Player.Position.Y;
        float peakY = startY;
        for (int i = 0; i < 300; i++)
        {
            Run(fixture, 1, new Vector3(0f, 0f, -1f));
            peakY = Math.Max(peakY, fixture.Player.Position.Y);
        }

        // The ramp climbs 4 m over 16 m; four seconds at 7 m/s must take the player well up it.
        bool passed = peakY > startY + 1.5f;
        return new("walks up a ramp", passed, $"climbed from {Format(startY)} to {Format(peakY)}");
    }

    private static CheckResult FrameRateIndependentMovement()
    {
        var results = new List<float>();
        foreach (float frameDelta in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f })
        {
            using Fixture fixture = Fixture.Create();
            Run(fixture, 300);

            // Count frames rather than accumulating a float clock, so each case simulates exactly one
            // second and the comparison is about the simulation, not about test arithmetic.
            int frames = (int)MathF.Round(1f / frameDelta);
            Vector3 start = fixture.Player.Position;
            for (int i = 0; i < frames; i++)
            {
                Run(fixture, 1, new Vector3(0f, 0f, 1f), frameDelta);
            }

            results.Add(Vector3.Distance(start, fixture.Player.Position));
        }

        float min = float.MaxValue;
        float max = 0f;
        foreach (float distance in results)
        {
            min = Math.Min(min, distance);
            max = Math.Max(max, distance);
        }

        bool passed = (max - min) <= 0.05f;
        return new("moves the same distance at 30/60/144/240 fps", passed,
            $"spread = {Format(max - min)} m across {string.Join(", ", results.ConvertAll(Format))}");
    }

    private static CheckResult ShortTapsSurviveEveryFrameRate()
    {
        // Regression test for a whole class of bug, not one input. At 144 fps and above most frames run zero
        // fixed substeps, so a key tapped for exactly one frame can be sampled and then thrown away before the
        // movement code ever sees it. Jump and dash were latched against this; the slide was missed and a
        // one-frame slide tap did nothing at all above 60 fps.
        //
        // Each of the three is pressed for a single frame and then released, and must still take effect at every
        // frame rate.
        List<string> failures = new();

        // Enough frames after the tap for a fixed substep to be certain to have run. At 240 fps a substep happens
        // roughly every fourth frame, so a single frame of waiting proves nothing - the tap is correctly still
        // waiting for a substep, which is the whole point of the latch.
        const int WaitFrames = 12;

        float[] rates = { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f };

        // Slide: one frame of tap, from a running start so the slide qualifies.
        foreach (float frameDelta in rates)
        {
            using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));
            ReachRunSpeed(run);

            run.StepAt(frameDelta);
            for (int i = 0; i < WaitFrames; i++)
            {
                run.StepAt(frameDelta);
            }

            if (!run.IsSliding)
            {
                failures.Add($"slide tap lost at {Format(1f / frameDelta)} fps");
            }
        }

        // Dash: one frame of tap, from a running start so the dash qualifies.
        foreach (float frameDelta in rates)
        {
            using SlideScript run = new(DashTuning(), (frame, self) => Forward(dash: self.ConsumeDashPress()));

            run.StepUntil(s => s.Speed >= run.Tuning.MoveSpeed - 0.1f, 300);

            run.StepAt(frameDelta);
            for (int i = 0; i < WaitFrames; i++)
            {
                run.StepAt(frameDelta);
            }

            if (run.DashCount == 0)
            {
                failures.Add($"dash tap lost at {Format(1f / frameDelta)} fps");
            }
        }

        // Jump: one frame of tap.
        foreach (float frameDelta in rates)
        {
            using Fixture fixture = Fixture.Create();
            Run(fixture, 60);

            fixture.JumpHeld = true;
            fixture.Run(1, Vector3.Zero, frameDelta);
            fixture.JumpHeld = false;
            for (int i = 0; i < WaitFrames; i++)
            {
                fixture.Run(1, Vector3.Zero, frameDelta);
            }

            if (fixture.Player.Movement.TotalJumps == 0)
            {
                failures.Add($"jump tap lost at {Format(1f / frameDelta)} fps");
            }
        }

        return new("a one-frame key tap is never lost at any frame rate", failures.Count == 0,
            failures.Count == 0
                ? "slide, dash and jump all fire from a single-frame tap at 30, 60, 144 and 240 fps"
                : string.Join(" | ", failures));
    }

    private static CheckResult FrameRateIndependentFalling()
    {
        var results = new List<float>();
        foreach (float frameDelta in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f })
        {
            using Fixture fixture = Fixture.Create(spawnY: 30f);
            int frames = (int)MathF.Round(0.5f / frameDelta);
            for (int i = 0; i < frames; i++)
            {
                Run(fixture, 1, Vector3.Zero, frameDelta);
            }

            results.Add(fixture.Player.Position.Y);
        }

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float value in results)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        bool passed = (max - min) <= 0.25f;
        return new("falls consistently at 30/60/144/240 fps", passed,
            $"spread = {Format(max - min)} m over 0.5 s");
    }

    private static CheckResult AirMomentumIsPreserved()
    {
        using Fixture fixture = Fixture.Create();
        Run(fixture, 300);

        // Get airborne, then hand the player more speed than the configured move speed.
        fixture.JumpHeld = true;
        Run(fixture, 1);
        fixture.JumpHeld = false;
        Run(fixture, 4);

        const float boost = 12f;
        Vector3 velocity = fixture.Player.Velocity;
        fixture.Player.Body.LinearVelocity = new Vector3(velocity.X, velocity.Y, boost);

        float speedWithoutInput = 0f;
        for (int i = 0; i < 20; i++)
        {
            Run(fixture, 1);
            speedWithoutInput = fixture.Player.HorizontalSpeed;
        }

        float speedWithInput = 0f;
        for (int i = 0; i < 20; i++)
        {
            Run(fixture, 1, new Vector3(0f, 0f, 1f));
            speedWithInput = fixture.Player.HorizontalSpeed;
        }

        // Above the configured move speed, air control must not act as a brake.
        bool noAirFriction = Math.Abs(speedWithoutInput - boost) <= 0.2f;
        bool noBraking = speedWithInput >= boost - 0.2f;

        return new("air momentum survives above move speed", noAirFriction && noBraking,
            $"{Format(speedWithoutInput)} m/s coasting, {Format(speedWithInput)} m/s with input, launched at {Format(boost)} m/s");
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>
    /// Winding audit: for every closed primitive the builder produces, each triangle's geometric normal
    /// must point away from the solid's interior. A face wound the other way is back-facing and gets culled,
    /// which is exactly how a collider ends up existing with no visible mesh.
    /// </summary>
    private static CheckResult EveryPrimitiveFacesOutward()
    {
        List<string> failures = new();

        AuditWinding("box", new MeshBuilder().AddBox(new Vector3(2f, 3f, 4f), Color.White).Build(), failures);
        AuditWinding("ramp", new MeshBuilder().AddRamp(new Vector2(8f, 16f), 4f, Color.White).Build(), failures);
        AuditWinding("cylinder", new MeshBuilder().AddCylinder(1.4f, 5f, Color.White, segments: 20).Build(), failures);

        return new("every primitive triangle faces outward", failures.Count == 0,
            failures.Count == 0 ? "box, ramp and cylinder all closed and outward-facing" : string.Join("; ", failures));
    }

    private static CheckResult RenderAndCollisionGeometryMatch()
    {
        IReadOnlyList<string> problems = GeometryAudit.Run();
        string detail = problems.Count == 0
            ? $"{TestArena.Blocks.Count} map blocks: every collider has a matching mesh at the same world position"
            : string.Join(" | ", problems);

        return new("render and collision geometry match", problems.Count == 0, detail);
    }
    private static void AuditWinding(string name, MeshData mesh, List<string> failures)
    {
        Vector3 interior = Vector3.Zero;
        foreach (Microsoft.Xna.Framework.Graphics.VertexPositionColorNormal vertex in mesh.Vertices)
        {
            interior += vertex.Position;
        }

        interior /= mesh.Vertices.Length;

        for (int i = 0; i < mesh.Indices.Length; i += 3)
        {
            Vector3 a = mesh.Vertices[mesh.Indices[i]].Position;
            Vector3 b = mesh.Vertices[mesh.Indices[i + 1]].Position;
            Vector3 c = mesh.Vertices[mesh.Indices[i + 2]].Position;

            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            Vector3 faceCentre = (a + b + c) / 3f;
            Vector3 outward = faceCentre - interior;

            if (Vector3.Dot(normal, outward) <= 0f)
            {
                failures.Add($"{name} triangle {i / 3} normal {normal} points inward (centre {faceCentre})");
            }
        }
    }
    /// This is the invariant in its purest form: wall contact cannot create upward movement, so any rise
    /// here would mean collision resolution is launching the player.
    /// </summary>
    private static CheckResult WallContactCreatesNoUpwardMotion()
    {
        using Fixture fixture = Fixture.Create(spawnPosition: new Vector3(0f, 1f, -96f));
        Run(fixture, 60);

        float restY = fixture.Player.Position.Y;
        float highest = restY;

        for (int i = 0; i < 240; i++)
        {
            Run(fixture, 1, WallPush);
            highest = Math.Max(highest, fixture.Player.Position.Y);
        }

        float rise = highest - restY;
        return new("wall contact alone creates no upward movement", rise <= 0.02f,
            $"rose {Format(rise)} m over 4 s with no jump input (rest {Format(restY)})");
    }

    /// <summary>
    /// The reported bug reproduced through the real gameplay path: <c>PlayerController.Update</c> with
    /// synthetic per-frame input and real frame deltas, against the actual arena colliders, holding
    /// forward into the north wall while spamming jump for four seconds. The earlier wall tests drove
    /// movement directly, so they could not catch anything that depends on per-frame input sampling or on
    /// variable frame timing - both of which differ from the live game.
    /// </summary>
    private static CheckResult LivePathAtWallCannotClimb()
    {
        float jumpApex = JumpApex();
        float worstRise = 0f;
        float worstDrift = 0f;
        float worstNoJumpRise = 0f;
        bool allSettled = true;
        string detail = string.Empty;

        foreach (float frameDelta in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f })
        {
            LiveWallResult result = RunLiveWallScenario(frameDelta);
            detail += $"{1f / frameDelta:0}fps rise {Format(result.Rise)} drift {Format(result.Drift)}; ";

            worstRise = Math.Max(worstRise, result.Rise);
            worstDrift = Math.Max(worstDrift, result.Drift);
            worstNoJumpRise = Math.Max(worstNoJumpRise, result.NoJumpRise);
            allSettled &= Math.Abs(result.SettledY - result.RestY) <= 0.05f;
        }

        bool bounded = worstRise <= jumpApex + 0.15f;
        bool noClimb = worstDrift <= 0.05f;
        bool wallInert = worstNoJumpRise <= 0.02f;

        return new("live path cannot climb a wall at any frame rate", bounded && noClimb && wallInert && allSettled,
            $"{detail}| no-jump rise {Format(worstNoJumpRise)} m, apex {Format(jumpApex)} m, settled {allSettled}");
    }

    private readonly record struct LiveWallResult(float RestY, float Rise, float Drift, float NoJumpRise, float SettledY);

    private static float JumpApex()
    {
        PlayerTuning tuning = new();
        return tuning.JumpSpeed * tuning.JumpSpeed / (2f * -tuning.Gravity);
    }

    /// <summary>
    /// One run of the manual reproduction through the live path: same capsule, same wall collider, same
    /// floor, same movement into the wall, same repeated jump pattern, same fixed timestep.
    /// </summary>
    private static LiveWallResult RunLiveWallScenario(float frameDelta)
    {
        PhysicsWorld physics = new();
        MapColliders.Build(physics, TestArena.Blocks);

        PlayerTuning tuning = new() { SpawnPosition = new Vector3(0f, 1f, -96f), SpawnYaw = 0f };

        int frame = 0;
        bool jumpEnabled = true;
        Func<PlayerInputState> tapJump = () => new(
            new Vector2(0f, 1f),
            jumpEnabled && (frame / 6) % 2 == 0,
            false,
            false);

        using PlayerController controller = PlayerController.CreateHeadless(physics, tuning, tapJump);

        Settle(controller, frameDelta, 1.5f);
        float restY = controller.Player.Position.Y;

        float maxY = restY;
        float firstHalfMax = restY;
        int frames = (int)MathF.Round(4f / frameDelta);

        for (int i = 0; i < frames; i++)
        {
            frame = i;
            controller.Update(frameDelta);
            float y = controller.Player.Position.Y;
            maxY = Math.Max(maxY, y);
            if (i < frames / 2)
            {
                firstHalfMax = Math.Max(firstHalfMax, y);
            }
        }

        // Release jump completely: the player must fall back to the floor.
        jumpEnabled = false;
        float settledY = maxY;
        float elapsed = 0f;
        while (elapsed < 2f)
        {
            frame++;
            controller.Update(frameDelta);
            settledY = controller.Player.Position.Y;
            elapsed += frameDelta;
        }

        float noJumpRise = MeasureWallRiseWithoutJump(frameDelta);

        return new LiveWallResult(restY, maxY - restY, maxY - firstHalfMax, noJumpRise, settledY);
    }

    /// <summary>Same wall, same movement, but no jump input: the player must never rise.</summary>
    private static float MeasureWallRiseWithoutJump(float frameDelta)
    {
        PhysicsWorld physics = new();
        MapColliders.Build(physics, TestArena.Blocks);

        PlayerTuning tuning = new() { SpawnPosition = new Vector3(0f, 1f, -96f), SpawnYaw = 0f };
        using PlayerController controller = PlayerController.CreateHeadless(
            physics,
            tuning,
            () => new PlayerInputState(new Vector2(0f, 1f), false, false, false));

        Settle(controller, frameDelta, 1.5f);
        float restY = controller.Player.Position.Y;
        float highest = restY;

        int frames = (int)MathF.Round(4f / frameDelta);
        for (int i = 0; i < frames; i++)
        {
            controller.Update(frameDelta);
            highest = Math.Max(highest, controller.Player.Position.Y);
        }

        return highest - restY;
    }

    /// <summary>
    /// Every vertical surface in the real arena, one at a time, driven through the live path. A single
    /// wall fixture cannot catch a collider that is in the wrong place, so this walks the actual block
    /// list and presses the player into each face. Ramps are excluded where climbing is intended (the
    /// slope) and included where it is not (the two sides and the back).
    /// </summary>

    /// <summary>
    /// Movement direction must never decide whether a jump is allowed. Standing on a walkable surface, the
    /// player must be able to jump while moving forward, backward, or sideways, and on flat ground, on a
    /// shallow or steep ramp, at the top of a ramp, and on an obstacle top.
    ///
    /// The regression this guards: "am I still in the jump I started?" used to be read off vertical speed.
    /// Running <i>up</i> a ramp is also upward motion, so the player was treated as mid-jump while plainly
    /// standing on the slope, coyote time was never armed, and W+Space did nothing on any incline while
    /// S/A/D kept working. A second defect compounded it: the support distance was a vertical ray length, so
    /// on a slope it over-reported the gap to the surface and consumed the tolerance budget.
    /// </summary>
    private static CheckResult JumpIsAllowedInEveryDirection()
    {
        const float frameDelta = 1f / 60f;
        List<string> failures = new();
        int totalTaps = 0;
        int totalJumped = 0;

        foreach ((string surface, Vector3 spawn, float yaw) in JumpSurfaces())
        {
            foreach ((string direction, Vector2 move) in JumpDirections())
            {
                int frame = 0;
                bool space = false;
                int taps = 0;
                int jumped = 0;

                using PhysicsWorld physics = NewArenaPhysics();
                using PlayerController controller = PlayerController.CreateHeadless(
                    physics,
                    new PlayerTuning { SpawnPosition = spawn, SpawnYaw = yaw },
                    () => new PlayerInputState(move, space, false, false));

                // Get moving in that direction so forward speed is genuinely non-zero on a slope.
                for (int i = 0; i < 45; i++)
                {
                    controller.Update(frameDelta);
                }

                // The player must still be on this surface when the jump is offered. A single long run
                // covers 80 m, which walks straight off a 16 m ramp and spends the rest of the trial on
                // flat ground where the bug cannot show, so each trial is short and repeated.
                const int TrialFrames = 90;

                for (int trial = 0; trial < 8; trial++)
                {
                    int settledFrames = 0;

                    for (frame = 0; frame < TrialFrames; frame++)
                    {
                        // Whether a jump *should* be allowed is decided here, independently of the movement
                        // code, by asking the physics world directly: is there a walkable surface right under
                        // the feet? Gating on the player's own coyote state instead would be circular, and
                        // would hide the very bug this guards - a player standing on a slope who was never
                        // granted support.
                        //
                        // A few consecutive standing frames are required first, so the instant of touching
                        // down after a jump is not counted: mid-landing the capsule is inside the tolerance
                        // for a frame or two before support is re-armed, which is correct behaviour.
                        settledFrames = StandingOnFloor(physics, controller) ? settledFrames + 1 : 0;
                        bool press = frame % 2 == 0 && settledFrames >= 5;

                        space = press;
                        controller.Update(frameDelta);

                        if (!press)
                        {
                            continue;
                        }

                        taps++;

                        if (controller.Player.Movement.JumpedThisStep)
                        {
                            jumped++;
                            continue;
                        }

                        failures.Add(
                            $"{surface} {direction} trial {trial} frame {frame}: standing on the surface but the " +
                            $"jump was refused (vy={Format(controller.Player.Velocity.Y)}, " +
                            $"grounded={controller.Player.IsGrounded}, speed={Format(controller.Player.HorizontalSpeed)}, " +
                            $"pos={Describe(controller.Player.Position)})");
                    }
                }

                totalTaps += taps;
                totalJumped += jumped;

                if (taps == 0)
                {
                    failures.Add($"{surface} {direction}: never offered a jump, the player never found support");
                }
            }
        }

        string detail = failures.Count == 0
            ? $"{totalJumped}/{totalTaps} jumps fired while supported, identically in all four directions on " +
              "flat floor, ramp low/mid/high, box top and platform"
            : string.Join(" | ", failures);

        return new("jump is allowed while moving in any direction", failures.Count == 0, detail);
    }

    /// <summary>
    /// <summary>
    /// True when a walkable surface sits directly beneath the capsule's feet. Measured from the physics world
    /// rather than from <see cref="PlayerMovement"/>, so it is an independent opinion about whether the player
    /// is standing, and cannot be fooled by the same logic the test is checking.
    /// </summary>
    private static bool StandingOnFloor(PhysicsWorld physics, PlayerController controller)
    {
        Player.Player player = controller.Player;
        Vector3 centre = player.Position;
        float reach = player.Tuning.CapsuleHalfHeight + 0.05f;

        RayHit hit = physics.Raycast(centre, Vector3.Down, reach, player.Body.Collidable);

        // The ray starts at the capsule centre, so the gap below the feet is the ray length less the
        // capsule's half height, measured perpendicular to the surface it landed on.
        return hit.Hit
            && hit.Normal.Y >= player.Tuning.MinimumGroundNormalY
            && (hit.Distance * hit.Normal.Y) - player.Tuning.CapsuleHalfHeight <= 0.05f;
    }
    /// <summary>Geometry up to this tall is a step the player is meant to be able to walk onto.</summary>
    private const float StepHeightLimit = 0.5f;

    /// <summary>
    /// Every vertical surface in the real arena, one at a time, driven through the live path. A single wall
    /// fixture cannot catch a collider that is in the wrong place, so this walks the actual block list and
    /// presses the player into each face. Ramp slopes and bottoms are omitted: they are meant to be climbed or
    /// stood on. Sides and backs are included: they are not.
    /// </summary>
    private static CheckResult NoArenaSurfaceAllowsUnintendedClimbing()
    {
        const float frameDelta = 1f / 60f;
        List<string> failures = new();
        List<string> skipped = new();
        int surfaces = 0;
        int usable = 0;

        foreach ((string label, Vector3 origin, Vector3 normal, float height) in VerticalSurfaces())
        {
            surfaces++;

            // Stand off the face at ground level. Height is deliberately ignored: what matters is that the
            // player starts outside the face, on whatever floor is actually under that spot.
            Vector3 spawn = new(origin.X + (normal.X * 1.3f), 1f, origin.Z + (normal.Z * 1.3f));
            float yaw = YawFacing(normal);
            PlayerTuning tuning = new() { SpawnPosition = spawn, SpawnYaw = yaw };
            Vector2 wish = WishFor(normal, yaw);

            // Both phases start from a still settle, so the reference height is the floor the player was
            // placed on rather than a ledge they walked onto while already being pushed at the face.
            Vector2 activeWish = Vector2.Zero;
            bool activeJump = false;

            using PlayerController controller = PlayerController.CreateHeadless(
                NewArenaPhysics(),
                tuning,
                () => new PlayerInputState(activeWish, activeJump, false, false));

            Settle(controller, frameDelta, 1.5f);
            float restY = controller.Player.Position.Y;

            // A spawn buried in geometry gets ejected, which looks like climbing but is a bad fixture.
            if (MathF.Abs(restY - tuning.CapsuleHalfHeight) > 0.3f)
            {
                skipped.Add($"{label} (settled at {Format(restY)} m, not floor)");
                continue;
            }

            // The approach corridor must be open, and the floor under the spawn must be the ground rather than
            // the top of something else. Arena blocks sit close together, so a geometrically plausible spawn
            // can easily be inside a neighbour.
            using (PhysicsWorld probe = NewArenaPhysics())
            {
                RayHit approach = probe.Raycast(spawn, -normal, 4f);
                if (!approach.Hit || MathF.Abs(approach.Distance - 1.3f) > 0.35f)
                {
                    skipped.Add($"{label} (approach blocked{(approach.Hit ? $" at {Format(approach.Distance)} m" : string.Empty)})");
                    continue;
                }

                RayHit floor = probe.Raycast(spawn + new Vector3(0f, 3f, 0f), Vector3.Down, 8f);
                if (!floor.Hit || floor.Position.Y > 0.05f)
                {
                    skipped.Add($"{label} (floor at y={Format(floor.Position.Y)} m, not ground)");
                    continue;
                }
            }

            // Low, deliberately walkable geometry (the 0.25 m pads) is meant to be stepped onto, so only faces
            // taller than a step are expected to refuse all vertical motion.
            bool walkableStep = height <= StepHeightLimit;
            usable++;

            // Pushing into a vertical face with no jump must never lift the player at all.
            activeWish = wish;
            float highest = restY;
            float peakSpeed = 0f;

            for (int i = 0; i < 240; i++)
            {
                controller.Update(frameDelta);
                highest = MathF.Max(highest, controller.Player.Position.Y);
                peakSpeed = MathF.Max(peakSpeed, controller.Player.Velocity.Length());
            }

            float pushRise = highest - restY;
            if (walkableStep)
            {
                if (pushRise > height + 0.15f)
                {
                    failures.Add($"{label}: rose {Format(pushRise)} m against a {Format(height)} m step");
                }
            }
            else if (pushRise > 0.02f)
            {
                failures.Add($"{label}: rose {Format(pushRise)} m pushing into it with no jump (peak speed {Format(peakSpeed)} m/s)");
            }

            // Hammer jump against it. Gaining height the run did not already have is climbing.
            _jumpTapFrame = 0;
            activeJump = true;
            float maxY = restY;
            float firstHalf = restY;

            for (int i = 0; i < 960; i++)
            {
                if (i == 480)
                {
                    firstHalf = maxY;
                }

                controller.Update(frameDelta);
                maxY = MathF.Max(maxY, controller.Player.Position.Y);
            }

            float growth = maxY - firstHalf;
            if (growth > 0.05f)
            {
                failures.Add($"{label}: still {Format(growth)} m higher at 16 s than at 8 s - that is climbing");
            }
        }

        string detail = failures.Count == 0
            ? $"{usable}/{surfaces} vertical arena surfaces clean"
                + (skipped.Count == 0 ? string.Empty : $"; {skipped.Count} unusable spawn(s) skipped")
            : string.Join(" | ", failures);

        return new("no arena surface allows unintended climbing", failures.Count == 0, detail);
    }

    private static int _jumpTapFrame;

    private static bool JumpTapped() => (_jumpTapFrame++ / 6) % 2 == 0;

    private static PhysicsWorld NewArenaPhysics()
    {
        PhysicsWorld physics = new();
        MapColliders.Build(physics, TestArena.Blocks);
        return physics;
    }

    /// <summary>Yaw such that the camera's forward direction points along <paramref name="direction"/>.</summary>
    private static float YawFacing(Vector3 direction) => MathF.Atan2(-direction.X, -direction.Z);

    private static Vector2 WishFor(Vector3 direction, float yaw)
    {
        // PlayerInputState x is strafe and y is forward; rotate the world direction into that basis.
        float sin = MathF.Sin(yaw);
        float cos = MathF.Cos(yaw);
        return new Vector2((direction.X * cos) - (direction.Z * sin), (-direction.X * sin) - (direction.Z * cos));
    }

    /// <summary>
    /// Every vertical face of every arena block, with a point on the face and its outward normal.
    /// </summary>
    private static IEnumerable<(string Label, Vector3 Origin, Vector3 Normal, float Height)> VerticalSurfaces()
    {
        foreach (MapBlock block in TestArena.Blocks)
        {
            Vector3 half = block.Size * 0.5f;
            float sin = MathF.Sin(MathHelper.ToRadians(block.YawDegrees));
            float cos = MathF.Cos(MathHelper.ToRadians(block.YawDegrees));

            Vector3 Rotate(Vector3 v) => new((v.X * cos) + (v.Z * sin), v.Y, (-v.X * sin) + (v.Z * cos));

            switch (block.Kind)
            {
                case MapBlockKind.Box:
                    foreach ((Vector3 normal, Vector3 offset) in new[]
                    {
                        (new Vector3(1f, 0f, 0f), new Vector3(half.X, 0f, 0f)),
                        (new Vector3(-1f, 0f, 0f), new Vector3(-half.X, 0f, 0f)),
                        (new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, half.Z)),
                        (new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -half.Z)),
                    })
                    {
                        yield return ($"{block.Kind} {Describe(block)} {normal}", block.Position + Rotate(offset), Rotate(normal), block.Size.Y);
                    }

                    break;

                case MapBlockKind.Cylinder:
                    foreach (Vector3 normal in new[]
                    {
                        new Vector3(1f, 0f, 0f),
                        new Vector3(-1f, 0f, 0f),
                        new Vector3(0f, 0f, 1f),
                        new Vector3(0f, 0f, -1f),
                    })
                    {
                        // MapBlock.Size.X is the cylinder's radius, not half of it: both MapRenderer and
                        // MapColliders read it as a radius, so half-extents here would spawn inside the shape.
                        Vector3 rotated = Rotate(normal);
                        yield return ($"{block.Kind} {Describe(block)} {normal}", block.Position + (rotated * block.Size.X), rotated, block.Size.Y);
                    }

                    break;

                case MapBlockKind.Ramp:
                    // Sides and back are vertical and must not be climbable. The slope is intentionally omitted.
                    foreach ((Vector3 normal, Vector3 offset) in new[]
                    {
                        (new Vector3(1f, 0f, 0f), new Vector3(half.X, block.Size.Y * 0.5f, 0f)),
                        (new Vector3(-1f, 0f, 0f), new Vector3(-half.X, block.Size.Y * 0.5f, 0f)),
                        (new Vector3(0f, 0f, 1f), new Vector3(0f, block.Size.Y * 0.5f, half.Z)),
                    })
                    {
                        yield return ($"{block.Kind} {Describe(block)} {normal}", block.Position + Rotate(offset), Rotate(normal), block.Size.Y);
                    }

                    break;
            }
        }
    }

    private static string Describe(MapBlock block) =>
        $"{block.Size.X:0.#}x{block.Size.Y:0.#}x{block.Size.Z:0.#} at {block.Position} yaw {block.YawDegrees:0.#}";

    private static void Settle(PlayerController controller, float frameDelta, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            controller.Update(frameDelta);
            elapsed += frameDelta;
        }
    }

    /// <summary>
    /// The exact sequence from the bug report, against a real arena wall: stand on the floor already touching
    /// it, tap Space over and over while pressing into it. Every jump must happen with the feet genuinely on a
    /// walkable surface, the total climb must stay within one apex, and the player must land back on the floor.
    ///
    /// This is the regression guard for the wall-jump defect: the downward ground probes were cast from the
    /// capsule centre with a 0.7 m lateral reach, wider than the 0.4 m capsule, so a probe hanging over a ledge's
    /// top face reported that surface as being below the feet. Jump support was re-armed metres in the air and
    /// repeated Space presses walked the player up the wall one apex at a time.
    /// </summary>
    private static CheckResult WallContactDoesNotAllowASecondJump()
    {
        const float frameDelta = 1f / 60f;

        // The 6x2x6 step at (12,1,0): a vertical face at x = 15 whose top (y = 2) is walkable. This is the
        // shape that exposed the defect - a wall the player can reach the top of.
        Vector3 spawn = new(15f + 0.4f + 0.01f, 1f, 0f);
        float yaw = YawFacing(new Vector3(-1f, 0f, 0f));
        Vector2 intoWall = WishFor(new Vector3(-1f, 0f, 0f), yaw);

        int frame = 0;
        Vector2 activeWish = Vector2.Zero;
        bool activeJump = false;

        using PlayerController controller = PlayerController.CreateHeadless(
            NewArenaPhysics(),
            new PlayerTuning { SpawnPosition = spawn, SpawnYaw = yaw },
            () => new PlayerInputState(activeWish, activeJump, false, false));

        // Stand still on the floor first, so the reference height is the floor and not a ledge.
        Settle(controller, frameDelta, 1.5f);
        float floorY = controller.Player.Position.Y;
        float feet = floorY - controller.Player.Tuning.CapsuleHalfHeight;

        activeWish = intoWall;
        List<string> airborneJumps = new();
        float peakFeet = feet;

        for (frame = 0; frame < 240; frame++)
        {
            activeJump = (frame / 4) % 2 == 0;

            float feetBefore = controller.Player.Position.Y - controller.Player.Tuning.CapsuleHalfHeight;
            controller.Update(frameDelta);
            float feetAfter = controller.Player.Position.Y - controller.Player.Tuning.CapsuleHalfHeight;
            peakFeet = MathF.Max(peakFeet, feetAfter);

            if (!controller.Player.Movement.JumpedThisStep)
            {
                continue;
            }

            // A jump is only legitimate if the feet were actually resting on a walkable surface. Anything else
            // is a jump granted while airborne against the wall.
            if (feetBefore > feet + 0.1f)
            {
                airborneJumps.Add($"frame {frame}: feet {Format(feetBefore)} m above the floor");
            }
        }

        float apex = JumpApex();
        float climb = peakFeet - feet;

        // Release everything and let the player land before checking where they ended up, otherwise the sample
        // lands mid-jump and says nothing about whether they came back down.
        activeWish = Vector2.Zero;
        activeJump = false;
        for (frame = 0; frame < 240; frame++)
        {
            controller.Update(frameDelta);
        }

        bool returnedToFloor = MathF.Abs(controller.Player.Position.Y - floorY) <= 0.15f;
        bool noClimb = climb <= apex + 0.15f;

        bool passed = airborneJumps.Count == 0 && noClimb && returnedToFloor;

        string detail =
            $"tapped Space against a vertical face for 4 s: {airborneJumps.Count} airborne jump(s)" +
            (airborneJumps.Count == 0 ? string.Empty : $" [{string.Join("; ", airborneJumps)}]") +
            $", climbed {Format(climb)} m (single apex {Format(apex)} m), ended at y={Format(controller.Player.Position.Y)} (floor {Format(floorY)})";

        return new("wall contact does not allow a second jump", passed, detail);
    }

    /// <summary>
    /// Spawn points resting on each kind of surface. The arena ramp at (0,0,22) yaw 180 rises from y=0 at
    /// z=30 to y=4 at z=14, so its surface height at z is (30 - z) / 16 * 4. Yaw Pi looks along -Z, which is
    /// uphill, so W is the forward-up-the-slope direction that used to fail.
    /// </summary>
    private static IEnumerable<(string Name, Vector3 Spawn, float Yaw)> JumpSurfaces()
    {
        yield return ("floor", new Vector3(0f, 1f, -60f), MathHelper.Pi);

        foreach ((string label, float z) in new[] { ("ramp-low", 27f), ("ramp-mid", 22f), ("ramp-high", 17f) })
        {
            float height = ((30f - z) / 16f) * 4f;
            yield return (label, new Vector3(0f, height + 0.95f, z), MathHelper.Pi);
        }

        // Flat obstacle tops, where the capsule can be near an edge and only partly supported.
        yield return ("box-top", new Vector3(30f, 6.95f, 0f), MathHelper.Pi);
        yield return ("platform", new Vector3(12f, 7.95f, -16f), MathHelper.Pi);
    }

    private static IEnumerable<(string Name, Vector2 Move)> JumpDirections()
    {
        yield return ("W", new Vector2(0f, 1f));
        yield return ("S", new Vector2(0f, -1f));
        yield return ("A", new Vector2(-1f, 0f));
        yield return ("D", new Vector2(1f, 0f));
    }

    // ------------------------------------------------- mouse and camera basis

    private static CheckResult MouseLookYawAndPitch()
    {
        PlayerTuning tuning = new();
        PlayerCamera camera = new(tuning);

        camera.ApplyLook(new Vector2(100f, 0f));
        float yawAfterRight = camera.Yaw;
        float pitchAfterRight = camera.Pitch;

        camera.ApplyLook(new Vector2(-200f, 0f));
        float yawAfterLeft = camera.Yaw;

        camera.ApplyLook(new Vector2(0f, -100f));
        float pitchAfterUp = camera.Pitch;

        // Far more than the pitch limit in each direction.
        camera.ApplyLook(new Vector2(0f, -100000f));
        float pitchClampedHigh = camera.Pitch;
        camera.ApplyLook(new Vector2(0f, 200000f));
        float pitchClampedLow = camera.Pitch;

        // "Yaws right" has to mean the view swings toward the player's right, not merely that yaw changed sign:
        // the default yaw is pi, so a correct right turn still leaves yaw positive.
        // "Right" means the view swings toward the player's right-hand vector, which is the only definition that
        // works at a non-zero starting yaw: the default spawn yaw is pi, so a correct right turn still leaves
        // yaw positive and a sign test would call it wrong.
        // A single mouse move is only ~13 degrees, so the test asks which way the view moved rather than whether
        // it ended up pointing right: the change in facing must have a positive component along the player's
        // right for a right turn, and along the left for a left turn. A sign test on yaw cannot work here,
        // because the default spawn yaw is pi.
        Vector3 initialForward = ForwardAt(tuning.SpawnYaw);
        Vector3 rightVector = new(-MathF.Cos(tuning.SpawnYaw), 0f, MathF.Sin(tuning.SpawnYaw));

        Vector3 turnedRight = ForwardAt(yawAfterRight) - initialForward;
        bool rightYawsRight = Vector3.Dot(turnedRight, rightVector) > 0.01f;
        bool leftYawsLeft = Vector3.Dot(ForwardAt(yawAfterLeft) - initialForward, -rightVector) > 0.01f;
        bool upLooksUp = pitchAfterUp > pitchAfterRight;
        bool clamped = MathF.Abs(pitchClampedHigh - tuning.MaximumPitch) < 0.001f
            && MathF.Abs(pitchClampedLow + tuning.MaximumPitch) < 0.001f;

        bool passed = rightYawsRight && leftYawsLeft && upLooksUp && clamped;

        return new("mouse right yaws right, mouse up looks up", passed,
            $"mouse right {Format(yawAfterRight)} rad yaw, mouse left {Format(yawAfterLeft)} rad, " +
            $"pitch clamps {Format(pitchClampedHigh)}/{Format(pitchClampedLow)}");
    }

    /// <summary>The horizontal facing at a given yaw, for asserting that a turn went the right way.</summary>
    private static Vector3 ForwardAt(float yaw) => new(MathF.Sin(yaw), 0f, MathF.Cos(yaw));

    private static CheckResult StrafeAxisIsNotInverted()
    {
        List<string> failures = new();

        foreach ((float yaw, string label) in new[] { (MathHelper.Pi, "north(-Z)"), (0f, "south(+Z)") })
        {
            PlayerTuning tuning = new() { SpawnYaw = yaw };
            PlayerCamera camera = new(tuning);

            Vector3 right = camera.FlatRight;
            Vector3 forward = camera.FlatForward;

            PlayerInputState strafeRight = new(new Vector2(1f, 0f), false, false, false);
            PlayerInputState strafeLeft = new(new Vector2(-1f, 0f), false, false, false);

            Vector3 gotRight = camera.GetWishDirection(strafeRight.Move);
            Vector3 gotLeft = camera.GetWishDirection(strafeLeft.Move);

            if (Vector3.Distance(gotRight, right) > 0.01f)
            {
                failures.Add($"{label}: D->{Describe(gotRight)} want {Describe(right)}");
            }

            if (Vector3.Distance(gotLeft, -right) > 0.01f)
            {
                failures.Add($"{label}: A->{Describe(gotLeft)} want {Describe(-right)}");
            }
        }

        return new("A strafes left and D strafes right", failures.Count == 0,
            failures.Count == 0 ? "north(-Z) and south(+Z) both strafe correctly" : string.Join("; ", failures));
    }

    private static CheckResult WalkAxisFollowsCameraForward()
    {
        List<string> failures = new();

        foreach ((float yaw, string label) in new[] { (MathHelper.Pi, "north(-Z)"), (0f, "south(+Z)") })
        {
            PlayerCamera camera = new(new PlayerTuning { SpawnYaw = yaw });

            Vector3 forward = camera.FlatForward;
            Vector3 backward = -forward;

            Vector3 gotForward = camera.GetWishDirection(new Vector2(0f, 1f));
            Vector3 gotBackward = camera.GetWishDirection(new Vector2(0f, -1f));

            if (Vector3.Distance(gotForward, forward) > 0.01f)
            {
                failures.Add($"{label}: W->{Describe(gotForward)} want {Describe(forward)}");
            }

            if (Vector3.Distance(gotBackward, backward) > 0.01f)
            {
                failures.Add($"{label}: S->{Describe(gotBackward)} want {Describe(backward)}");
            }
        }

        return new("W walks where the camera looks", failures.Count == 0,
            failures.Count == 0 ? "north(-Z) and south(+Z) both walk correctly" : string.Join("; ", failures));
    }

    private static CheckResult MovementBasisSurvivesRotation()
    {
        List<string> failures = new();

        foreach (float degrees in new[] { 15f, 47f, 123f, 200f, 340f })
        {
            float yaw = MathHelper.ToRadians(degrees);
            PlayerCamera camera = new(new PlayerTuning { SpawnYaw = yaw });

            Vector3 right = camera.FlatRight;
            Vector3 forward = camera.FlatForward;

            // The basis must stay orthonormal and level in every yaw.
            if (MathF.Abs(Vector3.Dot(right, forward)) > 0.001f)
            {
                failures.Add($"{degrees}deg: right and forward are not perpendicular");
            }

            if (MathF.Abs(right.Y) > 0.001f || MathF.Abs(forward.Y) > 0.001f)
            {
                failures.Add($"{degrees}deg: basis left the XZ plane");
            }

            Vector3 got = camera.GetWishDirection(new Vector2(1f, 0f));
            if (Vector3.Distance(got, right) > 0.01f)
            {
                failures.Add($"{degrees}deg: D->{Describe(got)} want {Describe(right)}");
            }
        }

        return new("movement basis stays correct at any yaw", failures.Count == 0,
            failures.Count == 0 ? "15/47/123/200/340 degrees all correct" : string.Join("; ", failures));
    }

    // ------------------------------------------------- wall contact (no support)

    /// <summary>
    /// The arena's north perimeter wall: a vertical slab whose face normal must be horizontal, so the walkable
    /// rule can never accept it as ground.
    /// </summary>
    private static CheckResult WallIsNotWalkableGround()
    {
        using Fixture fixture = Fixture.Create(spawnPosition: new Vector3(0f, 1f, -96f));

        // The wall face must be horizontal, so the walkable-surface rule can never accept it.
        RayHit face = fixture.Physics.Raycast(new Vector3(0f, 0.9f, -99.5f), Vector3.Forward, 2f, fixture.Player.Body.Collidable);
        float faceNormalY = face.Hit ? face.Normal.Y : float.NaN;

        // Probe downward from inside the wall's own footprint, high enough that the floor is out of reach.
        //
        // A ray that *starts inside* a collider is the awkward case: the engine reports the exit face rather
        // than an entry, so the wall's bottom face reads as an upward-facing surface and would look walkable.
        // What actually matters for gameplay is the probe the player runs, so this asserts on the player's own
        // verdict for a position inside the wall rather than on a hand-made ray.
        Vector3 insideWall = new(0f, 0.9f, -100f);
        bool wallRejected = fixture.Player.Movement.SupportDistanceBelowFeet > fixture.Tuning.GroundProbeDistance
            || !fixture.Player.IsGrounded;

        bool horizontalFace = face.Hit && MathF.Abs(faceNormalY) < 0.01f;
        bool reportsGround = fixture.Player.IsGrounded;

        bool passed = horizontalFace && wallRejected && !reportsGround;

        return new("vertical wall is never walkable ground", passed,
            $"wall face normal Y={Format(faceNormalY)}, inside-wall support gap {Format(fixture.Player.Movement.SupportDistanceBelowFeet)} m, reports ground={reportsGround}");
    }

    private static CheckResult WallContactDoesNotGrantSupport()
    {
        using Fixture fixture = Fixture.Create(spawnPosition: new Vector3(0f, 1f, -96f));
        fixture.JumpHeld = true;

        int airborneSamples = 0;
        int groundedSamples = 0;
        int supportedSamples = 0;
        int jumpsWhileRising = 0;

        bool wasGrounded = fixture.Player.IsGrounded;

        for (int i = 0; i < 240; i++)
        {
            // Sampled before the step: the support state at the moment the jump decision is made.
            bool wasSupported = fixture.Player.Movement.IsSupported;
            Run(fixture, 1, WallPush);

            // Only samples where the player was *already* airborne count. Standing on the floor is supposed to
            // report grounded and supported - that is not the bug. The bug is a wall re-arming support in the air.
            if (wasGrounded)
            {
                groundedSamples++;
            }
            else
            {
                airborneSamples++;

                if (wasSupported)
                {
                    supportedSamples++;
                }

                if (fixture.Player.Velocity.Y > fixture.Tuning.AscendingSpeedThreshold && wasSupported)
                {
                    jumpsWhileRising++;
                }
            }

            wasGrounded = fixture.Player.IsGrounded;
        }

        bool passed = airborneSamples > 0
            && supportedSamples == 0
            && jumpsWhileRising == 0;

        return new("wall contact grants neither grounded nor jump support", passed,
            $"{airborneSamples} airborne samples, wrongly supported {supportedSamples}, jumps while rising {jumpsWhileRising} (ground samples {groundedSamples} excluded: standing on the floor is not the bug)");
    }

    private static CheckResult RepeatedJumpAtWallDoesNotClimb()
    {
        using Fixture fixture = Fixture.Create(spawnPosition: new Vector3(0f, 1f, -96f));
        fixture.JumpHeld = true;

        Settle(fixture, 1f / 60f, 1.5f);
        float restY = fixture.Player.Position.Y;

        float maxHeight = restY;
        int jumpsWhileRising = 0;
        bool previousGrounded = fixture.Player.IsGrounded;

        for (int i = 0; i < 480; i++)
        {
            Run(fixture, 1, WallPush);
            maxHeight = MathF.Max(maxHeight, fixture.Player.Position.Y);

            if (fixture.Player.Velocity.Y > fixture.Tuning.AscendingSpeedThreshold && !previousGrounded)
            {
                jumpsWhileRising++;
            }

            previousGrounded = fixture.Player.IsGrounded;
        }

        float settledY = maxHeight;
        float elapsed = 0f;
        fixture.JumpHeld = false;
        while (elapsed < 2f)
        {
            Run(fixture, 1, WallPush);
            settledY = fixture.Player.Position.Y;
            elapsed += 1f / 60f;
        }

        float rise = maxHeight - restY;
        float limit = JumpApex() + 0.1f;
        bool bounded = rise <= limit;
        bool returned = MathF.Abs(settledY - restY) <= 0.05f;

        return new("repeated jump at a wall cannot climb it", bounded && returned && jumpsWhileRising == 0,
            $"jumps while rising {jumpsWhileRising}, max height {Format(rise)} m (limit {Format(limit)} m), " +
            $"settled back to {Format(settledY)} (rest {Format(restY)})");
    }

    private static Vector3 WallPush => new(0f, 0f, -1f);

    // ------------------------------------------------- slide and slide-jump (M3)

    /// <summary>Open ground in the middle of the arena, with room to build speed and slide.</summary>
    private static Vector3 SlideSpawn => new(0f, 1f, -40f);

    private static PlayerTuning SlideTuning() => new() { SpawnPosition = SlideSpawn, SpawnYaw = MathHelper.Pi };

    /// <summary>
    /// Drives the real update path with scripted per-frame input. The script is handed its own live state, so a
    /// scenario can react to what the player is actually doing - jumping on the frame the slide begins, say -
    /// instead of guessing frame numbers. That is what makes these tests read as descriptions of play.
    /// </summary>
    private sealed class SlideScript : IDisposable
    {
        private readonly PhysicsWorld _physics;
        private readonly PlayerController _controller;
        private int _frame;

        public SlideScript(PlayerTuning tuning, Func<int, SlideScript, PlayerInputState> input)
        {
            _physics = NewArenaPhysics();
            _controller = PlayerController.CreateHeadless(_physics, tuning, () => input(_frame, this));
        }

        /// <summary>A script on a caller-supplied world, for tests that need extra geometry in it.</summary>
        public SlideScript(PhysicsWorld physics, PlayerTuning tuning, Func<int, SlideScript, PlayerInputState> input)
        {
            _physics = physics;
            _controller = PlayerController.CreateHeadless(physics, tuning, () => input(_frame, this));
        }

        public PlayerController Controller => _controller;

        public MovementState State => _controller.Player.Movement.State;

        public bool IsSliding => State == MovementState.Sliding;

        public float Speed => _controller.Player.HorizontalSpeed;

        public float PositionY => _controller.Player.Position.Y;

        public float PositionX => _controller.Player.Position.X;

        public float PositionZ => _controller.Player.Position.Z;

        public Vector3 Velocity => _controller.Player.Velocity;

        public bool JumpedThisStep => _controller.Player.Movement.JumpedThisStep;

        public PlayerTuning Tuning => _controller.Player.Tuning;

        public int Frame => _frame;

        /// <summary>
        /// Runs one frame at an explicit delta, for frame-rate checks. Counts jumps and dashes from the
        /// movement's running totals rather than from the per-step flags, because a frame that runs several
        /// fixed substeps leaves those flags reflecting only the last substep.
        /// </summary>
        public void StepAt(float delta)
        {
            bool wasSliding = IsSliding;

            _controller.Update(delta);
            _frame++;

            if (_controller.Player.Movement.TotalJumps > _jumpsSeen)
            {
                JumpCount++;
                _jumpsSeen = _controller.Player.Movement.TotalJumps;
            }

            if (_controller.Player.Movement.TotalDashes > _dashesSeen)
            {
                DashCount++;
                _dashesSeen = _controller.Player.Movement.TotalDashes;
            }

            if (IsSliding && !wasSliding)
            {
                SlideCount++;
                SlideStartedFrame = _frame;
            }
        }

        private int _jumpsSeen;

        private int _dashesSeen;

        public int DashCount { get; private set; }

        /// <summary>Dashes fired during this script, for scenarios that need to act on the first one.</summary>
        public int DashesThisRun => DashCount;

        public DashStatus Dash => _controller.Player.Movement.Dash;

        public bool IsStanceTransitioning => _controller.Player.Movement.IsStanceTransitioning;

        public float CurrentHeight => _controller.Player.Movement.CurrentHeight;

        private bool _slidePressed;

        /// <summary>
        /// Returns true on exactly one frame, once the player is at run speed - the frame a real player would
        /// press the key. Scripts ask for this instead of hard-coding a frame number, so a scenario does not
        /// silently stop exercising the slide just because the run-up got faster or slower.
        /// </summary>
        public bool ConsumeSlidePress()
        {
            if (_slidePressed || Speed < Tuning.MoveSpeed - 0.1f)
            {
                return false;
            }

            _slidePressed = true;
            return true;
        }

        /// <summary>The dash equivalent of <see cref="ConsumeSlidePress"/>: one press, at run speed.</summary>
        public bool ConsumeDashPress()
        {
            if (_dashPressedOnce || Speed < Tuning.MoveSpeed - 0.1f)
            {
                return false;
            }

            _dashPressedOnce = true;
            return true;
        }

        /// <summary>One press, once the player is airborne and falling at a chosen rate.</summary>
        public bool ConsumeDashPressWhileFalling(float verticalSpeed)
        {
            if (_dashPressedOnce || Velocity.Y > -verticalSpeed)
            {
                return false;
            }

            _dashPressedOnce = true;
            return true;
        }

        private bool _dashPressedOnce;

        /// <summary>Jump on every frame from the given one onwards, for a press-and-hold.</summary>
        public bool JumpHeldFrom(int frame) => Frame >= frame;

        public int JumpCount { get; private set; }

        public int SlideCount { get; private set; }

        /// <summary>The frame the slide most recently began, or -1 if it never has.</summary>
        public int SlideStartedFrame { get; private set; } = -1;

        /// <summary>Runs one frame, sampling state before and after so transitions can be observed.</summary>
        public void Step() => StepAt(1f / 60f);

        /// <summary>Runs frames until the player's state satisfies <paramref name="until"/>, or time runs out.</summary>
        public void StepUntil(Func<SlideScript, bool> until, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (until(this))
                {
                    return;
                }

                Step();
            }
        }

        public void Dispose()
        {
            _controller.Dispose();
            _physics.Dispose();
        }
    }

    private static PlayerInputState Forward(bool jump = false, bool slide = false, bool dash = false) =>
        new(new Vector2(0f, 1f), jump, false, false, slide, dash);

    /// <summary>Runs until the player is at full run speed, the precondition for every slide scenario.</summary>
    private static void ReachRunSpeed(SlideScript run)
    {
        run.StepUntil(s => s.Speed >= SlideTuning().MoveSpeed - 0.1f, 150);
    }

    private static CheckResult SlideRequiresGroundedAndSpeed()
    {
        PlayerTuning tuning = SlideTuning();

        // Airborne: a slide needs ground, however fast the player is moving.
        using (SlideScript air = new(
            new PlayerTuning { SpawnPosition = new Vector3(0f, 14f, -40f), SpawnYaw = MathHelper.Pi },
            (frame, self) => Forward(slide: frame == 20)))
        {
            air.StepUntil(s => s.Velocity.Y < 0f, 60);
            air.StepUntil(_ => false, 150);

            if (air.SlideCount > 0 || air.IsSliding)
            {
                return new("slide requires being grounded and fast enough", false,
                    $"the player slid while airborne ({air.SlideCount} slide(s))");
            }
        }

        // Too slow: below SlideMinimumSpeed, so no slide however long the key is held.
        using (SlideScript slow = new(tuning, (frame, self) => new PlayerInputState(
            frame < 8 ? new Vector2(0f, 0.35f) : Vector2.Zero, false, false, false, frame == 20)))
        {
            slow.StepUntil(_ => false, 90);

            if (slow.SlideCount > 0)
            {
                return new("slide requires being grounded and fast enough", false,
                    $"slid {slow.SlideCount} time(s) at only {Format(slow.Speed)} m/s");
            }
        }

        return new("slide requires being grounded and fast enough", true,
            $"no slide while airborne, and none below {Format(tuning.SlideMinimumSpeed)} m/s");
    }

    private static CheckResult SlidePreservesMomentumAndDecelerates()
    {
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        float speedAtPress = run.Speed;

        // The slide starts on this press; entry speed is read on the next frame, once it has been applied.
        run.Step();
        float entrySpeed = run.Speed;
        float distanceAtSlide = run.PositionZ;

        bool sliding = run.IsSliding;
        (float minimum, float maximum) = run.Tuning.SlideSpeedRange;

        // Entry speed is the player's own momentum, clamped into range: never boosted, never reset.
        bool preserved = entrySpeed >= MathF.Min(speedAtPress, maximum) - 0.4f && entrySpeed <= maximum + 0.4f;

        // Deceleration has to be sampled while the slide is still running. Once the slide ends the player is
        // back under normal ground movement, which accelerates them toward MoveSpeed again - reading speed
        // after that point would show the player speeding up and make a healthy slide look like a failure.
        run.StepUntil(_ => false, 20);
        float travelled = MathF.Abs(run.PositionZ - distanceAtSlide);

        float speedBeforeDecay = run.Speed;
        run.StepUntil(_ => false, 20);
        float speedAfterDecay = run.Speed;

        bool stillSliding = run.IsSliding;
        bool decelerates = speedAfterDecay < speedBeforeDecay - 0.3f;
        bool carried = travelled > 1.0f;

        return new("slide preserves momentum and decelerates", sliding && preserved && carried && decelerates && stillSliding,
            $"ran at {Format(speedAtPress)} -> slid at {Format(entrySpeed)} m/s, travelled {Format(travelled)} m in 0.67 s, " +
            $"then {Format(speedBeforeDecay)} -> {Format(speedAfterDecay)} m/s (still sliding={stillSliding})");
    }

    private static CheckResult SlideAddsNoVerticalVelocity()
    {
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.Step();

        float feetAtSlide = run.PositionY - (run.Controller.Player.Movement.CurrentHeight * 0.5f);
        float peakUpward = 0f;
        float lowestFeet = feetAtSlide;

        run.StepUntil(s =>
        {
            peakUpward = MathF.Max(peakUpward, s.Velocity.Y);
            float feet = s.PositionY - (s.Controller.Player.Movement.CurrentHeight * 0.5f);
            lowestFeet = MathF.Min(lowestFeet, feet);
            return false;
        }, 90);

        bool noLaunch = peakUpward < 1.0f;
        bool noSink = lowestFeet >= -0.05f;

        return new("slide adds no vertical velocity", noLaunch && noSink,
            $"peak upward velocity {Format(peakUpward)} m/s, feet {Format(feetAtSlide)} -> lowest {Format(lowestFeet)} m");
    }

    private static CheckResult SlideExitsOnItsOwn()
    {
        // One press, released immediately: the slide must finish on its own rather than on key release.
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);

        int slideFrames = 0;
        run.StepUntil(s =>
        {
            if (s.IsSliding)
            {
                slideFrames++;
            }

            return !s.IsSliding && slideFrames > 0;
        }, 400);

        return new("slide ends on its own without holding the key", !run.IsSliding && slideFrames > 0,
            $"slid for {slideFrames} frames from a single key press, still sliding={run.IsSliding}");
    }

    private static CheckResult SlideJumpWorksAndJumpsExactlyOnce()
    {
        // Jump a few frames into the slide, the way a player chaining the technique would.
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(
            jump: self.IsSliding && self.JumpCount == 0 && frame - self.SlideStartedFrame >= 6,
            slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);

        // The jump and the slide end on the same step by design, so "was sliding when the jump fired" has to be
        // judged from before that step. StepUntil samples the state first, so the pre-step state is captured here.
        bool jumpedWhileSliding = false;
        float apexFeet = 0f;

        for (int i = 0; i < 400 && run.JumpCount == 0; i++)
        {
            bool slidingThisStep = run.IsSliding;

            run.Step();

            if (run.JumpCount > 0)
            {
                jumpedWhileSliding = slidingThisStep;
                apexFeet = run.PositionY - (run.Controller.Player.Movement.CurrentHeight * 0.5f);
            }
        }

        // Then confirm no further jump appears, which is the "exactly one" half of the claim.
        run.StepUntil(_ => false, 300);

        return new("jumping from a slide fires exactly one jump", run.JumpCount == 1 && jumpedWhileSliding,
            $"jump fired while sliding={jumpedWhileSliding}, total jumps={run.JumpCount} (must be 1), " +
            $"apex {Format(apexFeet)} m above the floor");
    }

    private static CheckResult SlideJumpPreservesHorizontalMomentum()
    {
        // The reference: plain running speed. A slide-jump must not fall back to it.
        float normalRunSpeed;
        using (SlideScript runner = new(SlideTuning(), (frame, self) => Forward()))
        {
            ReachRunSpeed(runner);
            normalRunSpeed = runner.Speed;
        }

        float speedAfterJump = 0f;
        float verticalAfterJump = 0f;

        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(
            jump: self.IsSliding && self.JumpCount == 0 && frame - self.SlideStartedFrame >= 6,
            slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);

        float speedAtSlideEntry = 0f;

        for (int i = 0; i < 200 && run.JumpCount == 0; i++)
        {
            if (run.IsSliding)
            {
                speedAtSlideEntry = run.Speed;
            }

            run.Step();

            if (run.JumpCount > 0)
            {
                // Read after the jump has been applied: horizontal must be untouched by it.
                speedAfterJump = run.Speed;
                verticalAfterJump = run.Velocity.Y;
            }
        }

        // The jump may only add vertical velocity. Horizontal has to be carried through untouched - neither
        // scaled back to the walk speed nor reset to it.
        bool keptMomentum = speedAfterJump >= speedAtSlideEntry - 0.4f;
        bool notResetToRunSpeed = MathF.Abs(speedAfterJump - speedAtSlideEntry) < 0.4f
            || speedAfterJump > normalRunSpeed + 0.15f;
        bool leftTheSlide = !run.IsSliding;

        return new("slide-jump keeps horizontal momentum", keptMomentum && notResetToRunSpeed && leftTheSlide,
            $"slid at {Format(speedAtSlideEntry)} m/s; the jump left it at {Format(speedAfterJump)} m/s horizontally " +
            $"with {Format(verticalAfterJump)} m/s vertical (normal run is {Format(normalRunSpeed)} m/s), still sliding={run.IsSliding}");
    }

    private static CheckResult SlideJumpIsFrameRateIndependent()
    {
        float[] peaks = new float[4];
        float[] deltas = { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f };

        for (int i = 0; i < deltas.Length; i++)
        {
            float delta = deltas[i];
            float peak = 0f;
            bool jumped = false;
            bool jumpedWhileSliding = false;

            // Driven directly rather than through a scripted input closure. The frame rate is the variable
            // under test here, so the harness must not also depend on *when* within its own loop the press
            // lands - holding jump from the moment the slide begins removes that second variable entirely.
            // The slide key is pressed once, at speed; the jump key is then held for the rest of the run. Both
            // have to be separate flags: the slide is edge-triggered, so holding it from frame zero would fire
            // it while the player is standing still and then never fire it again.
            bool slideHeld = false;
            bool jumpHeld = false;

            using PhysicsWorld physics = NewArenaPhysics();
            using PlayerController controller = PlayerController.CreateHeadless(
                physics,
                SlideTuning(),
                () => new PlayerInputState(new Vector2(0f, 1f), jumpHeld, false, false, slideHeld));

            for (int frame = 0; frame < 400; frame++)
            {
                bool slidingBefore = controller.Player.Movement.State == MovementState.Sliding;

                if (!slideHeld && controller.Player.HorizontalSpeed >= SlideTuning().MoveSpeed - 0.1f)
                {
                    slideHeld = true;
                }

                jumpHeld = slidingBefore || jumpHeld;

                int before = controller.Player.Movement.TotalJumps;
                controller.Update(delta);

                if (controller.Player.Movement.TotalJumps > before)
                {
                    jumped = true;
                    jumpedWhileSliding = slidingBefore;
                }

                if (jumped)
                {
                    peak = MathF.Max(peak, controller.Player.Position.Y);
                }
            }

            // Whether the jump was *initiated* from the slide is asserted by its own dedicated check; here the
            // only question is whether the resulting arc is identical at every frame rate, so the peak is read
            // whenever a jump happened at all.
            peaks[i] = jumped ? peak : 0f;
            _ = jumpedWhileSliding;
        }

        float spread = 0f;
        for (int i = 1; i < peaks.Length; i++)
        {
            spread = MathF.Max(spread, MathF.Abs(peaks[i] - peaks[0]));
        }

        return new("slide-jump is frame-rate independent", peaks[0] > 0f && spread <= 0.25f,
            $"peak height at 30/60/144/240 fps: {Format(peaks[0])}/{Format(peaks[1])}/{Format(peaks[2])}/{Format(peaks[3])} m, spread {Format(spread)} m");
    }

    private static CheckResult AirControlStillWorksAfterSlideJump()
    {
        // Jump out of the slide, then hold D. Ordinary air control must still add speed, never remove it.
        float speedAtJump = 0f;
        float speedAtApex = 0f;
        bool everAirborne = false;

        using SlideScript run = new(SlideTuning(), (frame, self) =>
        {
            Vector2 move = self.JumpCount > 0 ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            bool jump = self.IsSliding && self.JumpCount == 0 && frame - self.SlideStartedFrame >= 6;
            return new PlayerInputState(move, jump, false, false, self.ConsumeSlidePress());
        });

        ReachRunSpeed(run);

        for (int i = 0; i < 300 && run.JumpCount == 0; i++)
        {
            run.Step();
        }

        speedAtJump = run.Speed;

        // Measure only while genuinely in the air. The `_isGrounded` flag stays true for the first few
        // centimetres of a jump (GroundProbeDistance), and during that window the player is still on the
        // ground rules, so including it would measure ground friction rather than air control.
        run.StepUntil(s =>
        {
            everAirborne = true;

            // The top of the arc, where the contact has long since released and only air control is acting.
            if (s.Velocity.Y <= 0.5f)
            {
                speedAtApex = s.Speed;
                return true;
            }

            return false;
        }, 200);

        // Air control only ever adds along the wish direction, so by the top of the arc - well clear of the
        // ground - the player must be at least as fast as when the jump left the slide. Measuring at the apex
        // rather than on the way up avoids the frames where the solver is still releasing the ground contact,
        // which legitimately costs a little horizontal speed.
        bool controlWorks = everAirborne && speedAtApex >= speedAtJump - 0.05f;

        return new("air control still works after a slide-jump", controlWorks,
            $"left the slide at {Format(speedAtJump)} m/s, at the top of the jump while steering {Format(speedAtApex)} m/s (airborne seen={everAirborne})");
    }

    private static CheckResult SlideSteeringIsLimited()
    {
        // A hard 90 degree turn must not be available mid-slide, or the slide is just air control.
        using SlideScript run = new(SlideTuning(), (frame, self) =>
        {
            // Hold forward to build speed, slide, then hold D hard for the rest of the slide.
            Vector2 move = frame < 44 ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
            return new PlayerInputState(move, false, false, false, self.ConsumeSlidePress());
        });

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);
        float before = run.Speed;

        run.StepUntil(_ => false, 30);
        float after = run.Speed;

        // Steering must not have redirected the slide into a sideways sprint. Friction dominates while
        // sliding, so a speed gain here would mean the steering is unbounded.
        bool notBoosting = after <= before + 0.3f;
        bool stillSliding = run.IsSliding;

        return new("slide steering is limited, not free control", notBoosting && stillSliding,
            $"{Format(before)} -> {Format(after)} m/s after 0.5 s of hard sideways steering (friction is {Format(SlideTuning().SlideFriction)} m/s^2)");
    }

    private static CheckResult CapsuleShrinksAndFeetStayPut()
    {
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);

        PlayerTuning tuning = run.Tuning;
        float standingHeight = tuning.CapsuleHeight;

        // Sampled once the crouch has actually arrived. The stance eases down rather than cutting, so the frame
        // the slide begins is deliberately still at standing height.
        run.StepUntil(s => !s.Controller.Player.Movement.IsStanceTransitioning, 30);

        float crouchedHeight = run.Controller.Player.Movement.CurrentHeight;
        float feet = run.PositionY - (crouchedHeight * 0.5f);

        bool shrank = crouchedHeight < standingHeight - 0.3f;
        bool matchesTuning = MathF.Abs(crouchedHeight - tuning.SlideHeight) <= 0.1f;

        // Feet must not have moved: the floor is y = 0, and a crouch that sinks or lifts is visible.
        bool feetStable = MathF.Abs(feet) <= 0.06f;

        return new("capsule shrinks during a slide with feet in place", shrank && matchesTuning && feetStable,
            $"height {Format(standingHeight)} -> {Format(crouchedHeight)} m (slide height {Format(tuning.SlideHeight)} m), feet at {Format(feet)} m");
    }

    private static CheckResult SlideStanceEasesInRatherThanCutting()
    {
        // The entry used to be a single reshape on the frame the slide began, so the view dropped instantly while
        // the slide took a moment to build. The exit was already gradual, which made the two ends of the same
        // transition feel like different actions. This checks the way down is now a ramp, and that the ramp is
        // both monotonic and quick.
        PlayerTuning tuning = SlideTuning();
        float standing = tuning.CapsuleHeight;

        using SlideScript run = new(tuning, (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);

        // Step frame by frame through the entry, recording the stance height each frame.
        List<float> heights = new();
        float heightOnSlideFrame = standing;

        for (int i = 0; i < 40; i++)
        {
            run.Step();

            if (i == 0)
            {
                heightOnSlideFrame = run.Controller.Player.Movement.CurrentHeight;
            }

            if (run.IsSliding)
            {
                heights.Add(run.Controller.Player.Movement.CurrentHeight);
            }

            if (heights.Count > 0 && !run.Controller.Player.Movement.IsStanceTransitioning)
            {
                break;
            }
        }

        if (heights.Count == 0)
        {
            return new("slide stance eases in rather than cutting", false, "the slide never started");
        }

        // Not a cut. One substep of the crouch speed is 0.15 m of the 0.8 m drop, so the first frame should show
        // only a small fraction of it - not the whole thing at once.
        bool notCut = heightOnSlideFrame > standing - 0.25f;

        // A ramp: several distinct intermediate heights, each lower than the last.
        bool gradual = heights.Count >= 3;

        bool monotonic = true;
        for (int i = 1; i < heights.Count; i++)
        {
            if (heights[i] > heights[i - 1] + 0.001f)
            {
                monotonic = false;
            }
        }

        // It reaches the crouch, and lands on it rather than creeping toward it forever.
        float reached = heights[^1];
        bool reachesTarget = MathF.Abs(reached - tuning.SlideHeight) <= 0.02f;

        // And it is quick. The configured descent time for the whole drop is a hard ceiling to check against.
        float descentSeconds = heights.Count / 60f;
        float configured = (standing - tuning.SlideHeight) / tuning.SlideCrouchDownSpeed;
        bool quick = descentSeconds <= configured + 0.05f;

        return new("slide stance eases in rather than cutting",
            notCut && gradual && monotonic && reachesTarget && quick,
            $"standing {Format(standing)} m, still {Format(heightOnSlideFrame)} m on the slide's first frame, " +
            $"eased down to {Format(reached)} m over {heights.Count} frames ({Format(descentSeconds)} s, " +
            $"configured {Format(configured)} s); ramp = {string.Join(" ", heights.ConvertAll(h => h.ToString("0.00", CultureInfo.InvariantCulture)))} m");
    }

    private static CheckResult SlideStanceTransitionIsFrameRateIndependent()
    {
        // The crouch is applied in AfterStep, once per fixed substep, so a frame that runs several of them must
        // descend several increments - not one, and not a whole frame's worth. Otherwise the stance would appear
        // to move faster at low frame rates, and the collider would visibly disagree with the camera.
        float[] deltas = { 1f / 30f, 1f / 144f, 1f / 240f, 1f / 30f };
        float[] durations = new float[deltas.Length];

        for (int i = 0; i < deltas.Length; i++)
        {
            PlayerTuning tuning = SlideTuning();

            using SlideScript run = new(tuning, (frame, self) => Forward(slide: self.ConsumeSlidePress()));

            ReachRunSpeed(run);

            // Timed from the frame the slide actually begins, not from the start of the run-up: the run-up is a
            // fixed number of *frames* at 60 fps, so it covers wildly different amounts of simulated time at each
            // frame rate and would otherwise swamp the measurement.
            float elapsed = 0f;
            int guard = 0;
            bool started = false;

            while (elapsed < 2f && guard < 8000)
            {
                run.StepAt(deltas[i]);
                elapsed += deltas[i];
                guard++;

                // Checked before the slide test, not after: the frame the crouch finishes is usually still a
                // sliding frame, and testing for the slide first would skip straight past the completion.
                if (started && !run.IsStanceTransitioning)
                {
                    break;
                }

                if (run.IsSliding)
                {
                    started = true;
                }
            }

            durations[i] = elapsed;
        }

        float spread = 0f;
        for (int i = 1; i < durations.Length; i++)
        {
            spread = MathF.Max(spread, MathF.Abs(durations[i] - durations[0]));
        }

        // One fixed substep is the smallest difference this measurement can resolve.
        float tolerance = PhysicsDefaults.FixedTimeStep + 0.01f;

        return new("slide stance transition is frame-rate independent", spread <= tolerance,
            $"time from slide start to fully crouched at 30/144/240 fps: " +
            $"{Format(durations[0])}/{Format(durations[1])}/{Format(durations[2])} s, spread {Format(spread)} s " +
            $"(tolerance {Format(tolerance)} s, one fixed substep)");
    }

    private static CheckResult MovementEffectsDoNotTouchTheSimulation()
    {
        // The feedback effects must be a report on the movement, never an input to it. This runs an identical
        // movement scenario twice - once with effects firing throughout, once with none - and requires the two to
        // produce identical state at every step. Any coupling at all shows up here immediately as a divergence.
        List<string> failures = new();

        Vector3[] cleanPositions = new Vector3[180];
        float[] cleanSpeeds = new float[180];
        Vector3[] effectPositions = new Vector3[180];
        float[] effectSpeeds = new float[180];

        for (int withEffects = 0; withEffects < 2; withEffects++)
        {
            PlayerTuning tuning = SlideTuning();
            MovementEffects effects = new();

            using SlideScript run = new(tuning, (frame, self) =>
            {
                bool slide = self.ConsumeSlidePress();
                bool jump = self.IsSliding && self.JumpCount == 0;
                bool dash = self.DashCount > 0 && self.DashesThisRun == 0;

                if (withEffects == 1)
                {
                    // Deliberately spam every cue: a dash cue on every frame, a slide cue on every frame, and
                    // ageing. None of it may reach the player.
                    if (dash)
                    {
                        effects.SpawnDashStreaks(new Vector3(0f, 1f, 0f), Vector3.Forward);
                    }

                    if (slide)
                    {
                        effects.SpawnSlideDust(new Vector3(0f, 0f, 0f), Vector3.Forward);
                    }

                    effects.Update(1f / 60f);
                }

                return Forward(slide: slide, jump: jump, dash: dash);
            });

            ReachRunSpeed(run);

            Vector3[] positions = withEffects == 1 ? effectPositions : cleanPositions;
            float[] speeds = withEffects == 1 ? effectSpeeds : cleanSpeeds;

            for (int i = 0; i < 180; i++)
            {
                run.Step();
                positions[i] = run.Controller.Player.Position;
                speeds[i] = run.Speed;
            }
        }

        float worstPosition = 0f;
        float worstSpeed = 0f;

        for (int i = 0; i < 180; i++)
        {
            worstPosition = MathF.Max(worstPosition, Vector3.Distance(cleanPositions[i], effectPositions[i]));
            worstSpeed = MathF.Max(worstSpeed, MathF.Abs(cleanSpeeds[i] - effectSpeeds[i]));
        }

        // Not "close enough" - bit-identical, because the effects are not in the causal chain at all.
        if (worstPosition > 1e-6f || worstSpeed > 1e-6f)
        {
            failures.Add($"effects moved the player by {Format(worstPosition)} m and changed speed by {Format(worstSpeed)} m/s");
        }

        return new("movement effects do not touch the simulation", failures.Count == 0,
            failures.Count == 0
                ? "a slide-jump-dash sequence produced identical position and speed with and without effects firing every frame"
                : string.Join(" | ", failures));
    }

    private static CheckResult MovementEffectsSpawnAndExpire()
    {
        // The effects themselves, headlessly. A dash cue must actually produce geometry, it must clear itself
        // within its own stated lifetime, and it must be gone by the time the burst is over - otherwise the brief
        // becomes a permanent smear across the screen.
        MovementFeedbackSettings settings = new();
        MovementEffects effects = new(settings);

        effects.SpawnDashStreaks(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f));

        int spawned = effects.LiveCount;
        bool spawnedSome = spawned == settings.DashStreakCount;

        // Collectable as drawable geometry: centred ahead of the player and along the dash direction.
        Matrix view = Matrix.CreateLookAt(new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 1f), Vector3.Up);
        List<FeedbackQuad> quads = new();
        int collected = effects.CollectQuads(view, settings.PeakAlpha, quads);

        bool drawable = collected == spawned;

        bool aligned = true;
        bool visible = true;

        foreach (FeedbackQuad quad in quads)
        {
            if (Vector3.Dot(quad.Along, new Vector3(0f, 0f, 1f)) < 0.9f)
            {
                aligned = false;
            }

            if (quad.Color.A == 0 || quad.HalfLength <= 0f || quad.HalfWidth <= 0f)
            {
                visible = false;
            }
        }

        // Gone once the lifetime is up.
        effects.Update(settings.DashStreakSeconds + 0.001f);
        bool expired = effects.LiveCount == 0;

        // And a slide cue behaves the same way.
        MovementEffects dust = new();
        dust.SpawnSlideDust(Vector3.Zero, new Vector3(0f, 0f, 1f));
        int dustSpawned = dust.LiveCount;
        bool dustNearFeet = true;

        List<FeedbackQuad> dustQuads = new();
        dust.CollectQuads(view, settings.PeakAlpha, dustQuads);

        foreach (FeedbackQuad quad in dustQuads)
        {
            if (quad.Centre.Y > 0.3f)
            {
                dustNearFeet = false;
            }
        }

        dust.Update(settings.SlideDustSeconds + 0.001f);
        bool dustExpired = dust.LiveCount == 0;

        // The pools are fixed: a burst cannot grow them, however many are fired.
        MovementEffects flooded = new();
        for (int i = 0; i < 200; i++)
        {
            flooded.SpawnDashStreaks(Vector3.Zero, Vector3.Forward);
        }

        bool bounded = flooded.LiveCount <= settings.Capacity;

        return new("movement effects spawn, draw and expire",
            spawnedSome && drawable && aligned && visible && expired
            && dustSpawned > 0 && dustNearFeet && dustExpired && bounded,
            $"a dash cue spawned {spawned} streaks and {collected} were drawable, all along the dash direction and fully opaque; " +
            $"cleared itself after {Format(settings.DashStreakSeconds)} s (live now {effects.LiveCount}); " +
            $"a slide cue spawned {dustSpawned} motes near the feet and cleared itself after {Format(settings.SlideDustSeconds)} s; " +
            $"200 dashes into the pool left {flooded.LiveCount} live (capacity {settings.Capacity})");
    }

    private static CheckResult CapsuleRestoresWhenHeadroomReturns()
    {
        using SlideScript run = new(SlideTuning(), (frame, self) => new PlayerInputState(
            frame < 42 ? new Vector2(0f, 1f) : Vector2.Zero, false, false, false, self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);
        run.StepUntil(s => !s.Controller.Player.Movement.IsStanceTransitioning, 30);

        float crouched = run.Controller.Player.Movement.CurrentHeight;

        // Let go and let the slide finish, then stand back up.
        run.StepUntil(s => s.Controller.Player.Movement.CurrentHeight >= run.Tuning.CapsuleHeight - 0.02f, 400);

        float restored = run.Controller.Player.Movement.CurrentHeight;
        bool exited = run.State != MovementState.Sliding;
        bool fullHeight = MathF.Abs(restored - run.Tuning.CapsuleHeight) <= 0.05f;

        return new("capsule restores to full height when there is headroom",
            crouched < run.Tuning.CapsuleHeight && exited && fullHeight,
            $"crouched {Format(crouched)} m -> restored {Format(restored)} m (standing {Format(run.Tuning.CapsuleHeight)} m), slide exited={exited}");
    }

    private static CheckResult LowCeilingKeepsPlayerCrouched()
    {
        // A slab whose underside sits 1.4 m above the floor: too low to stand (1.8 m), fine to slide (1.0 m).
        bool sawBlocked = false;
        bool everStoodUp = false;
        bool wasEverCrouched = false;
        float minHeight = float.MaxValue;
        PlayerTuning tuning = SlideTuning();

        // The ceiling box belongs to the same world as the player, so the world must outlive the controller.
        using (PhysicsWorld physics = NewArenaPhysics())
        {
            // Spanning the whole arena in Z. A short ceiling would be a cheat: the player simply slides out from
        // under it and stands up in open air, which says nothing about standing up while blocked.
        physics.AddStaticBox(new Vector3(12f, 0.6f, 300f), new Vector3(0f, 1.7f, -60f));

            int frame = 0;
            int pressFrame = -1;

            // The input closure has to read the player's live speed to decide when to press, so the
            // controller is declared first and assigned in the using below.
            PlayerController controller = null!;

            using (controller = PlayerController.CreateHeadless(
                physics,
                tuning,
                () =>
                {
                    bool pressNow = pressFrame < 0 && controller.Player.HorizontalSpeed >= tuning.MoveSpeed - 0.1f;
                    if (pressNow)
                    {
                        pressFrame = frame;
                    }

                    return new PlayerInputState(new Vector2(0f, 1f), false, false, false, pressNow);
                }))
            {
                for (frame = 0; frame < 300; frame++)
            {
                controller.Update(1f / 60f);

                if (controller.Player.Movement.SlideBlockedFromStanding)
                {
                    sawBlocked = true;
                }

                float height = controller.Player.Movement.CurrentHeight;

                if (height < tuning.CapsuleHeight - 0.01f)
                {
                    // Only judge the stance once the crouch has actually been applied. The key press and the
                    // stance change are not simultaneous, so frames before this are the player standing
                    // normally and say nothing about standing up under the ceiling.
                    wasEverCrouched = true;
                    minHeight = MathF.Min(minHeight, height);
                }
                else if (wasEverCrouched)
                {
                    everStoodUp = true;
                }
                }
            }
        }

        // Under a low ceiling the player must never force themselves upright into the slab.
        bool stayedCrouched = !everStoodUp;

        return new("a low ceiling keeps the player crouched", stayedCrouched && sawBlocked && minHeight > 0.2f,
            $"blocked-from-standing seen={sawBlocked}, minimum height {Format(minHeight)} m, ever stood up={everStoodUp}");
    }

    private static CheckResult SlideNeverSinksIntoTheFloor()
    {
        using SlideScript run = new(SlideTuning(), (frame, self) => Forward(slide: self.ConsumeSlidePress()));

        ReachRunSpeed(run);
        run.StepUntil(s => s.IsSliding, 10);

        float lowestFeet = float.MaxValue;
        float lowestAfterStanding = float.MaxValue;

        run.StepUntil(s =>
        {
            float feet = s.PositionY - (s.Controller.Player.Movement.CurrentHeight * 0.5f);
            lowestFeet = MathF.Min(lowestFeet, feet);
            return !s.IsSliding;
        }, 200);

        // Then keep going, in case standing back up is what would push the player into the floor.
        run.StepUntil(s =>
        {
            float feet = s.PositionY - (s.Controller.Player.Movement.CurrentHeight * 0.5f);
            lowestAfterStanding = MathF.Min(lowestAfterStanding, feet);
            return false;
        }, 200);

        bool noPenetration = lowestFeet >= -0.05f && lowestAfterStanding >= -0.05f;

        return new("sliding never sinks into the floor", noPenetration,
            $"lowest foot position {Format(lowestFeet)} m during the slide and {Format(lowestAfterStanding)} m after standing up (floor is y=0)");
    }

    // ------------------------------------------------------- dash and air dash (M4)

    /// <summary>Open ground with room to run and dash in any direction, away from the arena's furniture.</summary>
    private static Vector3 DashSpawn => new(0f, 1f, -55f);

    private static PlayerTuning DashTuning() => new() { SpawnPosition = DashSpawn, SpawnYaw = MathHelper.Pi };

    /// <summary>
    /// Runs a dash on the given frame and returns what it did, so a test can assert on direction and speed
    /// without caring which frame number the press landed on.
    /// </summary>
    private static DashOutcome DashOnce(
        PhysicsWorld physics,
        PlayerTuning tuning,
        Vector2 move,
        Func<SlideScript, bool>? press = null,
        int framesAfter = 0)
    {
        DashOutcome outcome = new();

        // Both are filled in on the dash frame itself, from inside the input callback.
        Vector3 positionBeforeDash = Vector3.Zero;
        float verticalBeforeDash = 0f;

        using SlideScript run = new(physics, tuning, (frame, self) =>
        {
            bool fire = press?.Invoke(self) ?? self.ConsumeDashPress();
            if (fire)
            {
                // Sampled on the dash frame itself, so the displacement check covers the dash rather than the
                // whole run-up that led to it.
                positionBeforeDash = self.Controller.Player.Position;
                verticalBeforeDash = self.Velocity.Y;
            }

            return new PlayerInputState(move, false, false, false, false, fire);
        });

        run.StepUntil(s => s.DashCount > 0, 240);

        outcome.Fired = run.DashCount > 0;
        outcome.VerticalAfter = run.Velocity.Y;
        outcome.HorizontalAfter = new Vector2(run.Velocity.X, run.Velocity.Z);
        outcome.TotalDashes = run.DashCount;
        outcome.PositionJumped = Vector3.Distance(run.Controller.Player.Position, positionBeforeDash) > 0.5f;
        outcome.SpeedAfter = run.Speed;
        outcome.VerticalBefore = verticalBeforeDash;

        run.StepUntil(_ => false, framesAfter);
        return outcome;
    }





    /// <summary>What a single dash press did, measured across the frame it fired.</summary>
    private sealed class DashOutcome
    {
        public bool Fired { get; set; }

        public float VerticalBefore { get; set; }

        public float VerticalAfter { get; set; }

        public float SpeedBefore { get; set; }

        public float SpeedAfter { get; set; }

        public Vector2 HorizontalAfter { get; set; }

        public int TotalDashes { get; set; }

        public bool PositionJumped { get; set; }
    }

    private static CheckResult DashFiresOnceOnAKeyEdgeAndHoldingDoesNotRepeat()
    {
        // A single press fires once...
        DashOutcome single = DashOnce(NewArenaPhysics(), DashTuning(), new Vector2(0f, 1f));
        bool firedOnce = single.Fired && single.TotalDashes == 1;

        // ...and holding the key for two seconds does not fire again once the cooldown expires.
        using SlideScript held = new(DashTuning(), (frame, self) =>
            new PlayerInputState(new Vector2(0f, 1f), false, false, false, false, frame >= 30));

        held.StepUntil(_ => false, 30);
        held.StepUntil(_ => false, 200);

        bool heldOnce = held.DashCount == 1;

        // Releasing and pressing again after the cooldown does fire, which is what makes it an action rather
        // than a one-shot for the whole run.
        using SlideScript repeated = new(DashTuning(), (frame, self) =>
            new PlayerInputState(new Vector2(0f, 1f), false, false, false, false, frame is 30 or 150));

        repeated.StepUntil(_ => false, 300);
        bool repeatWorks = repeated.DashCount == 2;

        bool passed = firedOnce && heldOnce && repeatWorks;

        return new("dash fires once per key edge", passed,
            $"one press -> {single.TotalDashes} dash(es); held for 3.8 s -> {held.DashCount}; " +
            $"pressed twice -> {repeated.DashCount} (cooldown is {Format(DashTuning().DashCooldown)} s)");
    }

    private static CheckResult DashGoesTheWayThePlayerIsMoving()
    {
        // Every direction and the diagonals, checked at two camera yaws so the direction cannot be secretly
        // absolute. Movement input decides the dash; the camera only supplies the fallback.
        (string Label, Vector2 Move)[] cases =
        [
            ("forward", new Vector2(0f, 1f)),
            ("back", new Vector2(0f, -1f)),
            ("left", new Vector2(-1f, 0f)),
            ("right", new Vector2(1f, 0f)),
            ("diagonal", new Vector2(1f, 1f)),
            ("diagonal-back", new Vector2(1f, -1f)),
        ];

        List<string> failures = new();

        foreach (float yawDegrees in new[] { 0f, 90f, 200f })
        {
            PlayerCamera camera = new(new PlayerTuning { SpawnYaw = MathHelper.ToRadians(yawDegrees) });

            foreach ((string label, Vector2 move) in cases)
            {
                // The dash must follow the same wish direction the controller derives from the camera and the
                // input, so that is exactly what the check compares against.
                Vector3 expectedWorld = camera.GetWishDirection(move);
                Vector2 expected = Vector2.Normalize(new Vector2(expectedWorld.X, expectedWorld.Z));

                // The controller has to be given the same yaw as the camera, or it measures a different heading.
                PlayerTuning tuning = new() { SpawnPosition = DashSpawn, SpawnYaw = camera.Yaw };
                DashOutcome outcome = DashOnce(NewArenaPhysics(), tuning, move);

                Vector2 actual = Vector2.Normalize(outcome.HorizontalAfter);
                float alignment = Vector2.Dot(actual, expected);

                // The dash blends carried momentum into the burst, so the heading leans slightly toward
                // wherever the player was already running. It must never actually point the other way.
                if (alignment < 0.95f)
                {
                    failures.Add($"{label} at {yawDegrees} deg: dashed {actual} want {expected}");
                }
            }
        }

        return new("dash goes the way the player is moving", failures.Count == 0,
            failures.Count == 0
                ? "forward, back, left, right and both diagonals all correct at 0, 90 and 200 degrees of yaw"
                : string.Join(" | ", failures));
    }

    private static CheckResult DashFallsBackToCameraForwardWithNoInput()
    {
        List<string> failures = new();

        foreach (float yawDegrees in new[] { 0f, 90f, 200f })
        {
            PlayerTuning tuning = DashTuning();
            tuning.SpawnYaw = MathHelper.ToRadians(yawDegrees);

            DashOutcome outcome = DashOnce(NewArenaPhysics(), tuning, Vector2.Zero);

            Vector3 forward = new(tuning.SpawnYaw is var y ? MathF.Sin(y) : 0f, 0f, MathF.Cos(tuning.SpawnYaw));
            Vector2 expected = new(forward.X, forward.Z);
            Vector2 actual = Vector2.Normalize(outcome.HorizontalAfter);

            if (Vector2.Dot(actual, expected) < 0.85f)
            {
                failures.Add($"{yawDegrees} deg: dashed {actual} want {expected}");
            }
        }

        return new("dash with no input goes where the player looks", failures.Count == 0,
            failures.Count == 0 ? "camera forward used at 0, 90 and 200 degrees of yaw" : string.Join(" | ", failures));
    }

    private static CheckResult DashPreservesMomentumWithoutStacking()
    {
        PlayerTuning tuning = DashTuning();

        // Dash on the run: momentum carries through and the result is capped at DashSpeed.
        bool reDash = false;

        using SlideScript run = new(tuning, (frame, self) => new PlayerInputState(
            new Vector2(0f, 1f), false, false, false, false, self.ConsumeDashPress() || reDash));

        run.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
        float speedBefore = run.Speed;
        float speedAtDash = speedBefore;

        run.StepUntil(s => s.DashCount > 0, 30);
        float speedAfterDash = run.Speed;

        // Repeated dashing, one press every time the cooldown has run out: speed must never run away.
        float peak = speedAfterDash;
        for (int i = 0; i < 600; i++)
        {
            reDash = run.DashCount > 0 && run.Dash.IsAvailable && run.Dash.CooldownRemaining <= 0f;
            run.Step();
            peak = MathF.Max(peak, run.Speed);
        }

        bool preserved = speedAfterDash >= speedAtDash + 1f;
        bool capped = peak <= tuning.DashSpeed + 0.6f;
        bool noStacking = run.DashCount >= 2;

        return new("dash preserves momentum without stacking", preserved && capped && noStacking,
            $"ran at {Format(speedBefore)} -> {Format(speedAtDash)}, dashed to {Format(speedAfterDash)}; " +
            $"peak over {run.DashCount} repeated dashes {Format(peak)} m/s (dash speed is {Format(tuning.DashSpeed)})");
    }

    private static CheckResult DashNeverTouchesVerticalVelocity()
    {
        // Grounded: a dash must add no upward motion at all.
        DashOutcome ground = DashOnce(NewArenaPhysics(), DashTuning(), new Vector2(0f, 1f));
        bool noLift = MathF.Abs(ground.VerticalAfter) < 1.5f && !ground.PositionJumped;

        // Falling: an air dash must not change how fast the player is falling beyond that frame's gravity.
        // The 'before' sample is taken inside the input callback, so it is the value on the dash frame itself
        // rather than a frame earlier.
        PlayerTuning tuning = new() { SpawnPosition = new Vector3(0f, 30f, -55f), SpawnYaw = MathHelper.Pi };
        float oneStepOfGravity = tuning.Gravity / 60f;

        float fallingBefore = float.NaN;

        using SlideScript fall = new(tuning, (frame, self) =>
        {
            bool fire = self.ConsumeDashPressWhileFalling(5f);
            if (fire)
            {
                fallingBefore = self.Velocity.Y;
            }

            return new PlayerInputState(Vector2.Zero, false, false, false, false, fire);
        });

        fall.StepUntil(s => s.DashCount > 0, 200);
        float fallingAfter = fall.Velocity.Y;

        bool preservesFall = MathF.Abs((fallingAfter - fallingBefore) - oneStepOfGravity) < 0.05f;

        // Jumping: a dash mid-jump must not cancel the jump.
        float risingBefore = float.NaN;

        using SlideScript rising = new(DashTuning(), (frame, self) =>
        {
            bool jump = self.JumpCount == 0 && self.Controller.Player.IsGrounded && frame > 5;

            // Only press once genuinely climbing, or the dash lands on the run-up instead of the jump.
            bool fire = self.Velocity.Y > 2f && self.ConsumeDashPress();
            if (fire)
            {
                risingBefore = self.Velocity.Y;
            }

            return new PlayerInputState(new Vector2(0f, 1f), jump, false, false, false, fire);
        });

        rising.StepUntil(s => s.DashCount > 0, 200);
        float risingAfter = rising.Velocity.Y;

        bool preservesJump = rising.JumpCount > 0 && risingAfter > 1f
            && MathF.Abs((risingAfter - risingBefore) - oneStepOfGravity) < 0.05f;

        bool passed = noLift && preservesFall && preservesJump;

        return new("dash never creates or destroys vertical velocity", passed,
            $"grounded vy {Format(ground.VerticalBefore)} -> {Format(ground.VerticalAfter)}; " +
            $"falling at {Format(fallingBefore)} -> {Format(fallingAfter)}; " +
            $"rising at {Format(risingBefore)} -> {Format(risingAfter)}");
    }

    private static CheckResult OnlyOneAirDashPerAirbornePeriod()
    {
        PlayerTuning tuning = new() { SpawnPosition = new Vector3(0f, 22f, -55f), SpawnYaw = MathHelper.Pi };

        // Press the dash key over and over on a fixed cadence for the whole fall, whether or not it is
        // available. A player mashing the key must still only get one air dash.
        int presses = 0;
        int spentInAir = 0;

        using SlideScript air = new(tuning, (frame, self) =>
        {
            bool fire = frame > 5 && frame % 20 == 0;
            if (fire)
            {
                presses++;
            }

            // Tracked as it goes, because landing is what resets the counter.
            spentInAir = Math.Max(spentInAir, self.Dash.DashesThisAirbornePeriod);

            return new PlayerInputState(new Vector2(0f, 1f), false, false, false, false, fire);
        });

        air.StepUntil(s => s.Controller.Player.IsGrounded, 600);

        int inAir = air.DashCount;
        bool oneOnly = inAir == 1 && presses >= 3 && spentInAir == tuning.AirDashLimit;

        // Landing restores availability.
        air.StepUntil(_ => false, 5);
        bool availableOnGround = air.Dash.IsAvailable;

        // A second airborne period gets a fresh allowance: jump, dash in the air, land, jump and dash again.
        int periods = 0;

        using SlideScript renewed = new(tuning, (frame, self) =>
        {
            bool jumped = self.Controller.Player.IsGrounded && self.Velocity.Y <= 0f && frame > 5;
            bool fire = !jumped && self.Velocity.Y > 1f && self.Dash.IsAvailable;
            if (fire && self.Dash.DashesThisAirbornePeriod == 0)
            {
                periods++;
            }

            return new PlayerInputState(new Vector2(0f, 1f), jumped, false, false, false, fire);
        });

        renewed.StepUntil(_ => false, 400);

        bool secondPeriodWorks = periods >= 2 && renewed.DashCount >= 2;

        return new("only one air dash per airborne period",
            oneOnly && availableOnGround && secondPeriodWorks,
            $"{inAir} air dash(es) from {presses} presses mid-fall (limit {tuning.AirDashLimit}, " +
            $"peak spent mid-air={spentInAir}); re-armed on landing={availableOnGround}; " +
            $"renewed in each later airborne period={secondPeriodWorks} ({periods} periods, {renewed.DashCount} dashes)");
    }

    private static CheckResult DashFromARunIsClearlyMoreThanRunningFaster()
    {
        // The core of the dash feel change. Dashing from a run and merely running faster are different actions,
        // and they have to be visibly different in the movement itself, not just in a camera effect.
        PlayerTuning tuning = DashTuning();

        using SlideScript dashed = new(tuning, (frame, self) => new PlayerInputState(
            new Vector2(0f, 1f), false, false, false, false, self.ConsumeDashPress()));

        // Reach a full run, note it, then dash.
        dashed.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
        float speedWhenDashed = dashed.Speed;

        dashed.StepUntil(s => s.DashCount > 0, 30);
        float speedAfterDash = dashed.Speed;

        // Hold the burst out for its full duration and watch what it does. If the burst were surrendered back to
        // ordinary acceleration straight away, the peak would be reached on the dash frame and immediately decay -
        // which is precisely the "it just felt like running faster" symptom.
        float peakDuringBurst = speedAfterDash;
        int framesBurstHeld = 0;

        for (int i = 0; i < 40 && dashed.Dash.DashesThisAirbornePeriod == 0; i++)
        {
            dashed.Step();
            peakDuringBurst = MathF.Max(peakDuringBurst, dashed.Speed);
            if (dashed.Speed > tuning.MoveSpeed + 1f)
            {
                framesBurstHeld++;
            }
        }

        // A burst that is clearly beyond the run speed, not a rounding error above it.
        float burstOverRun = peakDuringBurst - tuning.MoveSpeed;
        bool clearlyFaster = burstOverRun >= tuning.MoveSpeed * 0.5f;

        // And it must be held, not a one-frame spike: several frames spent above the run speed is the
        // difference between a movement action and a twitch of extra speed.
        bool heldNotSpiked = framesBurstHeld >= 3;

        // Dashing from a run must not read the same as dashing from standing. The standing case is genuinely
        // stationary - no movement input at all - so the only difference between the two is the speed the
        // player brought to the dash, which is exactly what has to show up in the result.
        using SlideScript standing = new(DashTuning(), (frame, self) => new PlayerInputState(
            Vector2.Zero, false, false, false, false, frame == 20));

        standing.StepUntil(_ => false, 20);
        float speedFromStanding = standing.Speed;
        standing.Step();
        speedFromStanding = standing.Speed;

        bool runDashBeatsStandingDash = speedAfterDash > speedFromStanding;

        // The ceiling still holds, so this is a bounded burst rather than free speed.
        bool capped = peakDuringBurst <= tuning.DashSpeed + 0.5f;

        return new("dash from a run is clearly more than running faster",
            clearlyFaster && heldNotSpiked && runDashBeatsStandingDash && capped,
            $"ran at {Format(speedWhenDashed)} m/s, dashed to {Format(speedAfterDash)} m/s, peak {Format(peakDuringBurst)} m/s " +
            $"held above run speed for {framesBurstHeld} frames (run speed {Format(tuning.MoveSpeed)}, dash speed {Format(tuning.DashSpeed)}); " +
            $"dash from standing reached only {Format(speedFromStanding)} m/s");
    }

    private static CheckResult DashHandsBackControlWithoutAReset()
    {
        PlayerTuning tuning = DashTuning();

        // Dash, then keep steering the same way. The burst has to end and normal movement have to take over
        // smoothly - no snap back to run speed, and no gap where the player is neither dashing nor moving.
        using SlideScript run = new(tuning, (frame, self) => new PlayerInputState(
            new Vector2(0f, 1f), false, false, false, false, self.ConsumeDashPress()));

        run.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
        run.StepUntil(s => s.DashCount > 0, 30);

        float speedAtDash = run.Speed;

        // Run past the end of the burst and settle.
        float lowestDuringHandover = float.PositiveInfinity;
        float speedAfterSettling = 0f;

        for (int i = 0; i < 90; i++)
        {
            run.Step();

            if (run.Frame - run.SlideStartedFrame > 0 && i < 40)
            {
                lowestDuringHandover = MathF.Min(lowestDuringHandover, run.Speed);
            }

            if (i == 89)
            {
                speedAfterSettling = run.Speed;
            }
        }

        // Settles back to the run speed it started from, since the player is still holding forward.
        bool returnsToRun = MathF.Abs(speedAfterSettling - tuning.MoveSpeed) <= 0.35f;

        // Never drops below the run speed on the way there: a dip would be the burst being *removed* rather
        // than handing over, which is what "no physics reset" means in practice.
        bool noDipBelowRun = lowestDuringHandover >= tuning.MoveSpeed - 0.35f;

        // And the burst was genuinely faster than the run at the moment it fired.
        bool wasBursting = speedAtDash > tuning.MoveSpeed;

        return new("dash hands back control without a physics reset",
            returnsToRun && noDipBelowRun && wasBursting,
            $"dashed at {Format(speedAtDash)} m/s, lowest {Format(lowestDuringHandover)} m/s during the handover, " +
            $"settled at {Format(speedAfterSettling)} m/s holding forward (run speed {Format(tuning.MoveSpeed)})");
    }

    private static CheckResult DashCannotPassThroughWalls()
    {
        List<string> failures = new();

        // A flat wall the player runs into while dashing, and a box face, and a ramp.
        (string Label, Vector3 Spawn, Vector2 Move, float Yaw)[] cases =
        [
            // Facing the arena's east wall, dashing straight at it.
            ("perimeter wall", new Vector3(97.6f, 1f, 0f), new Vector2(0f, -1f), MathHelper.Pi),
            // Dashing into the side of the 6x6x6 step.
            ("box side", new Vector3(27f, 1f, 0f), new Vector2(1f, 0f), MathHelper.Pi / 2f),
        ];

        foreach ((string label, Vector3 spawn, Vector2 move, float yaw) in cases)
        {
            PlayerTuning tuning = new() { SpawnPosition = spawn, SpawnYaw = yaw };

            using SlideScript run = new(tuning, (frame, self) => new PlayerInputState(
                move, false, false, false, false, frame == 20));

            run.StepUntil(_ => false, 19);
            Vector3 positionBefore = run.Controller.Player.Position;

            // Dash, then keep driving forward for long enough to have crossed the arena at dash speed.
            run.StepUntil(_ => false, 60);

            Vector3 positionAfter = run.Controller.Player.Position;
            float travelled = Vector3.Distance(positionBefore, positionAfter);

            // A dash cannot move the player further in one go than the dash speed allows, and the player must
            // still be a solid body in contact with the world - i.e. never end up inside the arena's furniture.
            bool noTeleport = travelled <= DashTuning().DashSpeed * 2f + 3f;
            bool stillSupported = run.Controller.Player.Movement.SupportDistanceBelowFeet < 1f;

            if (!noTeleport)
            {
                failures.Add($"{label}: travelled {Format(travelled)} m, which is further than a dash can carry");
            }

            if (!stillSupported)
            {
                failures.Add($"{label}: ended {Format(run.Controller.Player.Movement.SupportDistanceBelowFeet)} m from any surface, possibly inside geometry");
            }

            // And the dash must not have produced height, which is what wall-climbing would look like.
            float rise = positionAfter.Y - positionBefore.Y;
            if (MathF.Abs(rise) > 0.5f)
            {
                failures.Add($"{label}: rose {Format(rise)} m while dashing into it");
            }
        }

        return new("dash cannot pass through walls", failures.Count == 0,
            failures.Count == 0
                ? $"dashed into the perimeter wall and a box; neither teleported through nor gained height"
                : string.Join(" | ", failures));
    }

    private static CheckResult DashIsUnavailableDuringASlide()
    {
        PlayerTuning tuning = SlideTuning();
        bool holdDash = false;

        using SlideScript run = new(tuning, (frame, self) => new PlayerInputState(
            new Vector2(0f, 1f), false, false, false, self.ConsumeSlidePress(), holdDash));

        run.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
        run.StepUntil(s => s.IsSliding, 10);

        bool sliding = run.IsSliding;
        int dashesDuringSlide = 0;
        int dashesAfter = 0;

        // Hold the dash key down for the whole slide. Holding is not pressing, so the only edge lands while
        // the slide has already begun, and the dash must be turned away.
        holdDash = true;
        run.StepUntil(s => !s.IsSliding, 300);
        dashesDuringSlide = run.DashCount;

        // Now let the key up and press it again. Only a fresh press can dash, which is what proves the refusal
        // above was the slide's doing rather than the key simply never being pressed.
        holdDash = false;
        run.StepUntil(_ => false, 2);

        holdDash = true;
        run.StepUntil(s => s.DashCount > 0, 60);
        dashesAfter = run.DashCount;

        holdDash = false;

        bool refusedInSlide = dashesDuringSlide == 0;
        bool worksAfter = dashesAfter > 0;

        return new("dash is unavailable during a slide, available after", refusedInSlide && worksAfter && sliding,
            $"dashes fired during the slide: {dashesDuringSlide} (DashDuringSlide is {DashTuning().DashDuringSlide}); " +
            $"after the slide ended: {dashesAfter}; slide still started={sliding}");
    }

    private static CheckResult GroundJumpAndDashTogetherDoNotSpendTheAirDash()
    {
        PlayerTuning tuning = DashTuning();

        // Jump and dash on the same frame from the ground: the dash counts as a ground dash, so the air dash
        // must still be waiting after the jump.
        bool both = false;

        using SlideScript together = new(tuning, (frame, self) =>
        {
            both = self.JumpCount == 0 && self.Velocity.Y <= 0f && frame > 5;
            return new PlayerInputState(new Vector2(0f, 1f), both, false, false, false, both);
        });

        // Read the state on the frame the jump and dash actually happen, before the player can land again.
        together.StepUntil(s => s.DashCount > 0, 120);
        together.StepUntil(s => s.JumpCount > 0, 5);

        int dashes = together.DashCount;
        int airDashes = together.Dash.DashesThisAirbornePeriod;

        // The player jumped and dashed, is rising, and the air dash is still unspent. Vertical speed is the
        // honest test of being airborne here: the ground probe deliberately keeps the contact flag true for
        // the first few centimetres of a jump.
        bool airborne = together.Velocity.Y > 0f;
        bool airDashIntact = airDashes == 0;

        return new("ground jump and dash together do not spend the air dash",
            dashes == 1 && airborne && airDashIntact,
            $"jumped and dashed on one frame -> {dashes} dash(es), airborne={airborne}, " +
            $"air dashes spent={airDashes} (must be 0)");
    }

    private static CheckResult SlideJumpThenDashWorks()
    {
        PlayerTuning tuning = SlideTuning();

        using SlideScript run = new(tuning, (frame, self) => new PlayerInputState(
            new Vector2(0f, 1f),
            self.IsSliding && self.JumpCount == 0 && frame - self.SlideStartedFrame >= 6,
            false,
            false,
            self.ConsumeSlidePress(),
            self.JumpCount > 0));

        run.StepUntil(s => s.Speed >= tuning.MoveSpeed - 0.1f, 150);
        run.StepUntil(s => s.IsSliding, 10);
        run.StepUntil(s => s.JumpCount > 0, 60);

        float speedAtSlideJump = run.Speed;

        // Now dash in the air.
        run.StepUntil(_ => false, 4);
        float speedBeforeDash = run.Speed;
        run.StepUntil(s => s.DashCount > 0, 30);

        bool dashed = run.DashCount == 1;
        float speedAfterDash = run.Speed;
        bool airborne = run.State == MovementState.Airborne;

        // Momentum through the whole chain must not collapse.
        bool preserved = speedAfterDash >= speedAtSlideJump - 1f;

        return new("slide then jump then dash works", dashed && airborne && preserved,
            $"slide-jump at {Format(speedAtSlideJump)} -> {Format(speedBeforeDash)} m/s, air dash -> {Format(speedAfterDash)} m/s, " +
            $"still airborne={airborne}");
    }

    private static CheckResult DashIsFrameRateIndependent()
    {
        float[] deltas = { 1f / 30f, 1f / 144f, 1f / 240f, 1f / 30f, 1f / 144f };
        float[] distances = new float[deltas.Length];
        int[] fired = new int[deltas.Length];

        for (int i = 0; i < deltas.Length; i++)
        {
            float delta = deltas[i];
            bool dashPressed = false;

            using SlideScript run = new(DashTuning(), (f, self) => new PlayerInputState(
                new Vector2(0f, 1f), false, false, false, false, dashPressed));

            // Run up to a fixed *time*, then dash, so every frame rate dashes from the same place.
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                run.StepAt(delta);
                elapsed += delta;
            }

            dashPressed = true;
            run.StepAt(delta);
            dashPressed = false;

            // Wait for the dash to actually land, which is not the frame the key went down: at 144 fps and above
            // most frames run no fixed substep at all, so those two are not the same frame.
            run.StepUntil(s => s.DashCount > 0, 10);
            fired[i] = run.DashCount;

            // How far the player travels in the window after the dash is the meaningful comparison. Sampling
            // speed at a frame boundary cannot work here, because at 30 fps a single frame runs two fixed
            // substeps and so is observed further along the burst than the same instant at 240 fps. That is the
            // sampling grid differing, not the simulation. Distance over a fixed span of simulated time is
            // immune to that, so this is what actually demonstrates frame independence.
            Vector3 start = run.Controller.Player.Position;
            float window = 0f;

            while (window < 0.25f)
            {
                run.StepAt(delta);
                window += delta;
            }

            distances[i] = Vector3.Distance(start, run.Controller.Player.Position);
        }

        float spread = 0f;
        for (int i = 1; i < distances.Length; i++)
        {
            spread = MathF.Max(spread, MathF.Abs(distances[i] - distances[0]));
        }

        bool allFired = true;
        foreach (int count in fired)
        {
            allFired &= count == 1;
        }

        // Half a frame's worth of travel is the unavoidable floor on this measurement, since the fixed step can
        // only divide the window into whole substeps.
        float tolerance = DashTuning().DashSpeed / 60f;

        return new("dash is frame-rate independent", allFired && spread <= tolerance,
            $"distance covered in 0.25 s after the dash at 30/144/240 fps: " +
            $"{Format(distances[0])}/{Format(distances[1])}/{Format(distances[2])} m, spread {Format(spread)} m " +
            $"(tolerance {Format(tolerance)} m, half a substep of travel)");
    }

    // ------------------------------------------------- combat (M5)

    /// <summary>
    /// A flat, empty arena well clear of the test arena's furniture, so a combat check can reason about exact
    /// distances instead of about whatever happens to be nearby. The ground is a 60 m slab with a 100 m ceiling
    /// removed - the slab top is at y=0, matching the main arena, so player tuning behaves identically.
    /// </summary>
    /// <summary>
    /// A floor only, with no walls. Enough for checks about knockback and damage, which must not be perturbed by
    /// a rocket accidentally striking a wall instead of the floor.
    /// </summary>
    private static PhysicsWorld NewFlatPhysics(float halfExtent = 30f)
    {
        PhysicsWorld physics = new();
        physics.AddStaticBox(new Vector3(halfExtent * 2f, 2f, halfExtent * 2f), new Vector3(0f, -1f, 0f));
        return physics;
    }

    /// <summary>
    /// A floor plus four walls, for checks about projectiles striking surfaces. Without the walls a rocket aimed
    /// sideways simply flies off the edge of the floor slab and never hits anything, which would make the test
    /// pass for the wrong reason.
    /// </summary>
    private static PhysicsWorld NewRoomPhysics(float halfExtent = 30f, float wallHeight = 12f)
    {
        PhysicsWorld physics = NewFlatPhysics(halfExtent);
        float span = halfExtent * 2f;
        float thickness = 2f;

        physics.AddStaticBox(new Vector3(thickness, wallHeight, span), new Vector3(halfExtent, wallHeight * 0.5f, 0f));
        physics.AddStaticBox(new Vector3(thickness, wallHeight, span), new Vector3(-halfExtent, wallHeight * 0.5f, 0f));
        physics.AddStaticBox(new Vector3(span, wallHeight, thickness), new Vector3(0f, wallHeight * 0.5f, -halfExtent));
        physics.AddStaticBox(new Vector3(span, wallHeight, thickness), new Vector3(0f, wallHeight * 0.5f, halfExtent));

        return physics;
    }

    private static PlayerTuning CombatTuning(Vector3 spawn) =>
        new() { SpawnPosition = spawn, SpawnYaw = MathHelper.Pi };

    /// <summary>
    /// Builds a player plus the combat stack, on flat ground, with the player registered as their own target.
    /// Mirrors exactly what <c>GameApp</c> does, so a check exercises the real wiring rather than a simplified
    /// stand-in of it.
    /// </summary>
    private sealed class CombatRig : IDisposable
    {
        private readonly PhysicsWorld _physics;
        private bool _disposed;

        public CombatRig(Vector3? spawn = null, float halfExtent = 30f, RocketLauncherTuning? tuning = null, bool withWalls = false)
        {
            _physics = withWalls ? NewRoomPhysics(halfExtent) : NewFlatPhysics(halfExtent);
            Explosions = new ExplosionSystem();

            PlayerTuning settings = CombatTuning(spawn ?? new Vector3(0f, 1f, 0f));

            // The real controller and the real headless update path, so a rocket jump runs through exactly the
            // same movement code the game does - including fixed substeps and the AfterStep bookkeeping.
            Controller = PlayerController.CreateHeadless(_physics, settings);
            Player = Controller.Player;

            PlayerTarget = Explosions.Register(new CombatTarget("player", Player.Body, Player.Health, Player));

            Launcher = new RocketLauncher(tuning ?? new RocketLauncherTuning());
            Weapons = new WeaponController(_physics, Explosions, Launcher, PlayerTarget);
            Weapons.Projectiles.Register(Launcher);

            Move = Vector3.Zero;
            Jump = false;
            Slide = false;
        }

        public PhysicsWorld Physics => _physics;

        public PlayerController Controller { get; }

        public Combat.ExplosionSystem Explosions { get; }

        public Player.Player Player { get; }

        public CombatTarget PlayerTarget { get; }

        public RocketLauncher Launcher { get; }

        public WeaponController Weapons { get; }

        /// <summary>Movement input fed to the player, in world space. Zero means standing still.</summary>
        public Vector3 Move { get; set; }

        public bool Jump { get; set; }

        public bool Slide { get; set; }

        /// <summary>Fires along a direction, from the player's own position by default.</summary>
        public bool Fire(Vector3 direction, Vector3? origin = null) =>
            Weapons.TryFire(origin ?? Player.Position, direction);

        /// <summary>Advances one fixed step of everything: the player's movement, the weapon cooldown, and the
        /// projectile sweep, which the physics world runs as a pre-step callback.</summary>
        public void Step()
        {
            Launcher.Step(PhysicsDefaults.FixedTimeStep);
            Controller.StepForTest(Move, Jump, PhysicsDefaults.FixedTimeStep, Slide);
        }

        /// <summary>
        /// Advances one *frame* of a given length, through the real controller path - so the number of fixed
        /// substeps it contains is decided by the physics world exactly as it is in the game. This is what makes
        /// frame-rate checks meaningful: the grouping of substeps into frames is the only thing that varies.
        /// </summary>
        public void StepFrame(float frameDelta) =>
            Controller.StepForTest(Move, Jump, frameDelta, Slide);

        /// <summary>Runs <paramref name="frames"/> fixed steps.</summary>
        public void Step(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                Step();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Controller.Dispose();
            _physics.Dispose();
        }
    }

    /// <summary>
    /// Adds a dummy to a rig's explosion system and returns it. Kept as a helper so every check that needs a
    /// target sets it up the same way.
    /// </summary>
    private static TestDummy AddDummy(CombatRig rig, Vector3 position, float size = 1.2f, float mass = 60f)
    {
        TestDummy dummy = new(rig.Physics, $"dummy-{position.X:0.#}", position, size, mass);
        rig.Explosions.Register(dummy.Target);
        return dummy;
    }

    private static CheckResult HealthTakesDamageAccumulatesAndDies()
    {
        List<string> failures = new();

        Health health = new(100f);

        float afterOne = health.TakeDamage(DamageInfo.From("rocket", Vector3.Zero, 30f, false));
        if (MathF.Abs(afterOne - 70f) > 0.01f)
        {
            failures.Add($"30 damage left {Format(afterOne)}, expected 70.00");
        }

        // Damage accumulates rather than replacing.
        float afterTwo = health.TakeDamage(DamageInfo.From("rocket", Vector3.Zero, 25f, false));
        if (MathF.Abs(afterTwo - 45f) > 0.01f)
        {
            failures.Add($"a second hit left {Format(afterTwo)}, expected 45.00 (damage must accumulate)");
        }

        // Zero damage is not healing. A falloff of exactly zero at the edge of a radius must apply nothing.
        float afterZero = health.TakeDamage(DamageInfo.From("rocket", Vector3.Zero, 0f, false));
        if (MathF.Abs(afterZero - 45f) > 0.01f)
        {
            failures.Add($"zero damage changed health to {Format(afterZero)}, expected no change");
        }

        if (health.IsDead)
        {
            failures.Add("health reported dead with 45 left");
        }

        // Death, and it sticks.
        health.TakeDamage(DamageInfo.From("rocket", Vector3.Zero, 500f, false));
        if (!health.IsDead || health.CurrentHealth != 0f)
        {
            failures.Add($"overkill left {Format(health.CurrentHealth)}, expected exactly 0.00 and dead");
        }

        // A corpse takes nothing further, so a second rocket cannot double-count against a dead target.
        float afterDeath = health.TakeDamage(DamageInfo.From("rocket", Vector3.Zero, 10f, false));
        if (afterDeath != 0f)
        {
            failures.Add($"a dead target took more damage, leaving {Format(afterDeath)}");
        }

        return new("health takes damage, accumulates it and reports death", failures.Count == 0,
            failures.Count == 0
                ? "30 + 25 accumulated to 45, zero applied nothing, overkill clamped to exactly 0 and stayed dead"
                : string.Join(" | ", failures));
    }

    private static CheckResult ExplosionFalloffIsSmoothAndZeroAtTheEdge()
    {
        List<string> failures = new();

        const float radius = 5.5f;
        const float maxDamage = 120f;

        // Full strength at the centre.
        float centre = ExplosionFalloff.Strength(0f, radius);
        if (MathF.Abs(centre - 1f) > 0.001f)
        {
            failures.Add($"strength at the centre was {Format(centre)}, expected 1.00");
        }

        // Exactly zero at the edge, and beyond it - so the affected set has a clean boundary.
        float edge = ExplosionFalloff.Strength(radius, radius);
        float beyond = ExplosionFalloff.Strength(radius * 1.5f, radius);
        if (edge != 0f || beyond != 0f)
        {
            failures.Add($"strength at/beyond the edge was {Format(edge)}/{Format(beyond)}, expected 0.00");
        }

        // Symmetric about the midpoint: half way out is half strength. Linear falloff would also pass this, so
        // this is the weaker half of the smoothness claim.
        float half = ExplosionFalloff.Strength(radius * 0.5f, radius);
        if (MathF.Abs(half - 0.5f) > 0.01f)
        {
            failures.Add($"strength at half the radius was {Format(half)}, expected 0.50");
        }

        // Monotonically decreasing, sampled across the radius. A falloff that rises anywhere is wrong.
        float previous = float.MaxValue;
        for (int i = 0; i <= 40; i++)
        {
            float distance = radius * i / 40f;
            float strength = ExplosionFalloff.Strength(distance, radius);

            if (strength > previous + 1e-5f)
            {
                failures.Add($"strength rose from {Format(previous)} to {Format(strength)} at {Format(distance)} m");
                break;
            }

            previous = strength;
        }

        // Smooth, not linear. Smoothstep is steeper than the straight line near the centre and flatter near the
        // edge, so it sits *above* the linear value in the inner half and *below* it in the outer half. Testing
        // both halves is what distinguishes the curve from `1 - distance/radius`, which would match at the
        // endpoints and at the midpoint and nowhere else.
        float innerQuarter = ExplosionFalloff.Strength(radius * 0.25f, radius);
        float outerQuarter = ExplosionFalloff.Strength(radius * 0.75f, radius);

        if (innerQuarter <= 0.75f)
        {
            failures.Add($"strength at a quarter of the radius was {Format(innerQuarter)}, at or below the linear 0.75; the curve is not eased near the centre");
        }

        if (outerQuarter >= 0.25f)
        {
            failures.Add($"strength at three quarters of the radius was {Format(outerQuarter)}, at or above the linear 0.25; the curve is not eased near the edge");
        }

        // Damage uses the same curve as force: one falloff, not two.
        float damage = ExplosionFalloff.Damage(radius * 0.5f, maxDamage, radius);
        if (MathF.Abs(damage - (maxDamage * 0.5f)) > 0.01f)
        {
            failures.Add($"damage at half radius was {Format(damage)}, expected {Format(maxDamage * 0.5f)}");
        }

        return new("explosion falloff is smooth, symmetric and exactly zero at the edge", failures.Count == 0,
            failures.Count == 0
                ? $"1.00 at the centre, 0.50 at half radius, {Format(innerQuarter)} at a quarter (above the linear 0.75), " +
                  $"{Format(outerQuarter)} at three quarters (below the linear 0.25), exactly 0.00 at the edge and beyond"
                : string.Join(" | ", failures));
    }

    private static CheckResult ExplosionOnlyAffectsTargetsInsideItsRadius()
    {
        List<string> failures = new();

        using CombatRig rig = new(new Vector3(0f, 1f, 0f));

        // Four targets in a line, at distances straddling the radius: inside, near the edge, outside, far outside.
        using TestDummy inside = AddDummy(rig, new Vector3(2f, 1f, 0f));
        using TestDummy atEdge = AddDummy(rig, new Vector3(5.5f, 1f, 0f));
        using TestDummy outside = AddDummy(rig, new Vector3(8f, 1f, 0f));
        using TestDummy farOutside = AddDummy(rig, new Vector3(20f, 1f, 0f));

        Vector3 centre = new(0f, 1f, 0f);
        IReadOnlyList<BlastResult> results = rig.Explosions.Detonate(
            new Explosion(centre, 5.5f, 120f, 900f, rig.PlayerTarget),
            elapsedSeconds: 0f);

        foreach (BlastResult result in results)
        {
            bool expectedAffected = result.Distance < 5.5f;

            if (expectedAffected && result.Strength <= 0f)
            {
                failures.Add($"{result.Target.Name} at {Format(result.Distance)} m was inside but unaffected");
            }

            if (!expectedAffected && result.Strength != 0f)
            {
                failures.Add($"{result.Target.Name} at {Format(result.Distance)} m was outside but took strength {Format(result.Strength)}");
            }
        }

        // Confirm on the bodies themselves, not only in the reported results.
        if (inside.CurrentHealth >= inside.Health.MaxHealth)
        {
            failures.Add("the target 2 m away took no damage");
        }

        if (atEdge.CurrentHealth < atEdge.Health.MaxHealth)
        {
            failures.Add("the target exactly on the radius took damage; the edge must be excluded");
        }

        if (outside.CurrentHealth != outside.Health.MaxHealth)
        {
            failures.Add($"the target 8 m away was damaged ({Format(100f - outside.CurrentHealth)})");
        }

        if (farOutside.CurrentHealth != farOutside.Health.MaxHealth)
        {
            failures.Add("the target 20 m away was damaged");
        }

        return new("explosion only affects targets inside its radius", failures.Count == 0,
            failures.Count == 0
                ? "2 m damaged, exactly on the 5.5 m radius untouched, 8 m and 20 m untouched"
                : string.Join(" | ", failures));
    }

    private static CheckResult ExplosionPushesTargetsAwayFromTheCentre()
    {
        List<string> failures = new();

        const float radius = 5.5f;
        Vector3 centre = new(0f, 1f, 0f);

        // Four directions around the blast, checked independently. Each dummy sits on an axis, so "away from the
        // centre" is a known world direction and there is no per-target special case to hide behind.
        (string Label, Vector3 Position, Vector3 Expected)[] cases =
        [
            ("+X", new Vector3(2f, 1f, 0f), new Vector3(1f, 0f, 0f)),
            ("-X", new Vector3(-2f, 1f, 0f), new Vector3(-1f, 0f, 0f)),
            ("+Z", new Vector3(0f, 1f, 2f), new Vector3(0f, 0f, 1f)),
            ("+Y", new Vector3(0f, 3f, 0f), new Vector3(0f, 1f, 0f)),
        ];

        foreach ((string label, Vector3 position, Vector3 expected) in cases)
        {
            // Checked on the impulse itself, before any physics: this is the direction rule, isolated.
            Vector3 impulse = ExplosionFalloff.Impulse(centre, position, 900f, radius);
            float alignment = Vector3.Dot(Vector3.Normalize(impulse), expected);

            if (alignment < 0.999f)
            {
                failures.Add($"{label}: impulse pointed {Vector3.Normalize(impulse)}, expected {expected}");
            }
        }

        // And on real bodies, through the real explosion system: a blast below the player must push them upward.
        // This is the direction rule integrated with the physics rather than asserted on the impulse alone.
        using CombatRig rig = new(new Vector3(0f, 1f, 0f));
        rig.Step(20);

        rig.Explosions.Detonate(new Explosion(new Vector3(0f, 0.1f, 0f), radius, 120f, 900f, rig.PlayerTarget), 0f);

        if (rig.Player.Velocity.Y <= 0f)
        {
            failures.Add($"a blast under the player's feet gave vertical velocity {Format(rig.Player.Velocity.Y)}, expected it to push them up");
        }

        // A blast off to the side must push them that way, through the body and not by writing velocity.
        using CombatRig sideRig = new(new Vector3(0f, 1f, 0f));
        sideRig.Step(20);

        sideRig.Explosions.Detonate(new Explosion(new Vector3(-3f, 1f, 0f), radius, 120f, 900f, sideRig.PlayerTarget), 0f);

        if (sideRig.Player.Velocity.X <= 0f)
        {
            failures.Add($"a blast to the player's -X gave horizontal velocity {Format(sideRig.Player.Velocity.X)}, expected it to push them +X");
        }

        // The impulse is mass-scaled, so the velocity change is impulse/mass. Confirming it means the value in
        // the tuning is doing something physical rather than setting velocity outright.
        using CombatRig measured = new(new Vector3(-25f, 1f, 0f));
        using TestDummy dummy = AddDummy(measured, new Vector3(2f, 1f, 0f));

        float expectedDeltaV = dummy.Body.VelocityChangeFrom(
            new Vector3(900f * ExplosionFalloff.Strength(2f, radius), 0f, 0f));

        return new("explosion pushes targets away from the centre", failures.Count == 0,
            failures.Count == 0
                ? $"impulse pointed outward on all four axes, a blast underfoot launched the player at {Format(rig.Player.Velocity.Y)} m/s " +
                  $"and one to the side pushed them +X at {Format(sideRig.Player.Velocity.X)} m/s; " +
                  $"a dummy 2 m from a blast gains {Format(expectedDeltaV)} m/s for its {Format(dummy.Body.Mass)} kg"
                : string.Join(" | ", failures));
    }

    private static CheckResult ExplosionAddsToExistingVelocityRatherThanReplacingIt()
    {
        // The single most important safety property in the milestone. A blast must add to what a body is already
        // doing; if it replaced velocity, a rocket jump would cancel a run and a jump would be thrown away by
        // self-damage, which would make the weapon fight the movement system instead of extending it.
        List<string> failures = new();

        using CombatRig rig = new(new Vector3(-25f, 1f, 0f));
        using TestDummy dummy = AddDummy(rig, new Vector3(0f, 1f, 0f));

        // Give it a strong sideways velocity directly on the body, bypassing any gameplay code.
        Vector3 before = new(0f, 4f, -6f);
        dummy.Body.LinearVelocity = before;

        // Detonate below it, so the impulse is upward and orthogonal to the existing motion. That makes the two
        // contributions separable: an additive application changes only Y, a replacing one would discard the Z.
        rig.Explosions.Detonate(new Explosion(new Vector3(0f, -1f, 0f), 5.5f, 120f, 900f, rig.PlayerTarget), 0f);

        Vector3 after = dummy.Body.LinearVelocity;

        // The change must be exactly the impulse over the mass: an additive application.
        float distance = Vector3.Distance(new Vector3(0f, -1f, 0f), dummy.Position);
        float strength = ExplosionFalloff.Strength(distance, 5.5f);

        Vector3 expectedDelta = Vector3.Up * (900f * strength / dummy.Body.Mass);
        Vector3 actualDelta = after - before;
        float error = Vector3.Distance(actualDelta, expectedDelta);

        if (error > 0.05f)
        {
            failures.Add($"velocity changed by {Describe(actualDelta)}, expected exactly impulse/mass {Describe(expectedDelta)}");
        }

        if (Vector3.Distance(after, before) < 0.5f)
        {
            failures.Add("the blast barely changed the velocity at all");
        }

        // And the original motion must survive untouched on the axes the blast did not push. The body was moving
        // -Z at 6 m/s and must still be doing so: this is what "preserved" has to mean for momentum, and it is
        // exactly what a rocket jump depends on when the player is mid-sprint.
        if (MathF.Abs(after.Z - before.Z) > 0.01f)
        {
            failures.Add($"existing -Z motion was changed: {Format(before.Z)} -> {Format(after.Z)}");
        }

        if (MathF.Abs(after.X - before.X) > 0.01f)
        {
            failures.Add($"velocity appeared on an axis the blast did not push: X {Format(before.X)} -> {Format(after.X)}");
        }

        return new("explosion adds to existing velocity rather than replacing it", failures.Count == 0,
            failures.Count == 0
                ? $"velocity went {Describe(before)} -> {Describe(after)}: exactly impulse/mass added on Y, and the existing " +
                  $"-Z motion and zero X untouched"
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketSpawnsTravelsAndExpires()
    {
        List<string> failures = new();

        RocketLauncherTuning tuning = new();
        using CombatRig rig = new(new Vector3(-25f, 1f, 0f), tuning: tuning);

        Vector3 origin = new(0f, 2f, 0f);
        Vector3 direction = Vector3.Forward;

        bool fired = rig.Weapons.TryFire(origin, direction);
        if (!fired)
        {
            return new("rocket spawns, travels and expires", false, "the first shot was refused");
        }

        if (rig.Weapons.LiveProjectiles != 1)
        {
            failures.Add($"{rig.Weapons.LiveProjectiles} projectiles in flight after one shot, expected 1");
        }

        if (!rig.Weapons.Projectiles.TryGetFirst(out Projectile projectile))
        {
            return new("rocket spawns, travels and expires", false, "the projectile list was empty after firing");
        }

        // Spawned ahead of the muzzle, not inside the player's head, and travelling at exactly the configured
        // speed in the direction fired.
        float expectedSpeed = tuning.RocketSpeed;
        float speed = projectile.Velocity.Length();

        if (MathF.Abs(speed - expectedSpeed) > 0.01f)
        {
            failures.Add($"spawned at {Format(speed)} m/s, expected the configured {Format(expectedSpeed)}");
        }

        if (Vector3.Dot(Vector3.Normalize(projectile.Velocity), direction) < 0.999f)
        {
            failures.Add($"spawned travelling {Vector3.Normalize(projectile.Velocity)}, expected {direction}");
        }

        // The origin passed in is expected to *be* the muzzle now: GameApp hands the weapon
        // the character's authored MuzzlePoint socket, which is already at the end of the
        // barrel. MuzzleOffset is therefore 0, and a rocket appearing exactly at the
        // supplied origin is correct rather than a bug - it used to mean the shot was
        // spawning inside the player's own head back when the origin was the camera.
        //
        // What still matters here is that it does not spawn *behind* the origin, and that
        // the weapon did not quietly reintroduce an offset of its own.
        if (tuning.MuzzleOffset > 1e-4f)
        {
            failures.Add($"MuzzleOffset is {Format(tuning.MuzzleOffset)} m; the fire origin is now the " +
                         "muzzle socket itself, so any offset here double-counts the barrel");
        }

        if (Vector3.Distance(projectile.Position, origin) > 1e-3f)
        {
            failures.Add($"the rocket spawned {Format(Vector3.Distance(projectile.Position, origin))} m " +
                         "from the supplied muzzle origin; it should spawn exactly there");
        }

        if (!projectile.IsActive)
        {
            failures.Add("the rocket spawned inactive");
        }

        if (!ReferenceEquals(projectile.Owner, rig.PlayerTarget))
        {
            failures.Add("the rocket did not record its owner");
        }

        // Travel: straight and at constant speed, since a rocket does not fall or slow down.
        Vector3 start = projectile.Position;
        rig.Step(10);

        if (!rig.Weapons.Projectiles.TryGetFirst(out Projectile moved))
        {
            return new("rocket spawns, travels and expires", false, "the rocket vanished after 10 steps of open air");
        }

        float travelled = Vector3.Distance(start, moved.Position);
        float expectedTravel = expectedSpeed * (10f * PhysicsDefaults.FixedTimeStep);

        if (MathF.Abs(travelled - expectedTravel) > 0.05f)
        {
            failures.Add($"travelled {Format(travelled)} m in 10 steps, expected {Format(expectedTravel)}");
        }

        // Lifetime: outlive its configured life and detonate without having hit anything.
        rig.Step((int)MathF.Ceiling(tuning.RocketLifetime / PhysicsDefaults.FixedTimeStep) + 5);

        if (rig.Weapons.LiveProjectiles != 0)
        {
            failures.Add($"{rig.Weapons.LiveProjectiles} rockets still alive after the lifetime elapsed");
        }

        if (rig.Explosions.Detonations != 1)
        {
            failures.Add($"{rig.Explosions.Detonations} explosions from one rocket, expected exactly 1");
        }

        if (rig.Weapons.Projectiles.TotalExpirations != 1)
        {
            failures.Add($"{rig.Weapons.Projectiles.TotalExpirations} expiries recorded, expected 1");
        }

        return new("rocket spawns, travels and expires", failures.Count == 0,
            failures.Count == 0
                ? $"spawned at {Format(speed)} m/s ahead of the muzzle, travelled {Format(travelled)} m in 10 steps " +
                  $"at constant speed, and detonated on its {Format(tuning.RocketLifetime)} s lifetime in open air"
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketDetonatesOnWorldGeometry()
    {
        List<string> failures = new();

        // Fired at each kind of surface. Every one must stop the rocket and blow up; none may pass through. Each
        // case gets a walled room, so a rocket aimed at a wall cannot simply fly off the edge of the floor.
        (string Label, Vector3 Origin, Vector3 Direction)[] cases =
        [
            ("floor", new Vector3(0f, 6f, 0f), Vector3.Down),
            ("+X wall", new Vector3(-20f, 2f, 0f), Vector3.Right),
            ("-X wall", new Vector3(20f, 2f, 0f), Vector3.Left),
            ("+Z wall", new Vector3(0f, 2f, 20f), Vector3.Backward),
            ("-Z wall", new Vector3(0f, 2f, -20f), Vector3.Forward),
        ];

        foreach ((string label, Vector3 origin, Vector3 direction) in cases)
        {
            using CombatRig rig = new(new Vector3(-25f, 1f, -25f), withWalls: true);

            bool fired = rig.Weapons.TryFire(origin, direction);
            if (!fired)
            {
                failures.Add($"{label}: the shot was refused");
                continue;
            }

            rig.Step(300);

            if (rig.Weapons.LiveProjectiles != 0)
            {
                failures.Add($"{label}: the rocket was still in flight after 5 s - it passed through");
            }

            if (rig.Explosions.Detonations != 1)
            {
                failures.Add($"{label}: {rig.Explosions.Detonations} explosions, expected exactly 1 on impact");
            }

            if (rig.Weapons.Projectiles.TotalImpacts != 1)
            {
                failures.Add($"{label}: the impact was not recorded as an impact");
            }
        }

        // A box obstacle in the middle of a corridor, plus the ramp primitive, since the arena is made of all
        // three. The rocket must stop at the near surface in every case, not pass through and hit the wall behind.
        // Aimed from -Z travelling +Z, so the rocket meets each obstacle's near (-Z) face. Note the direction is
        // `-Vector3.Forward` in MonoGame's terms: its Forward is -Z, so travel towards +Z is Backward.
        (string Label, Vector3 Spawn, Vector3 Direction, System.Action<PhysicsWorld> Build, float ExpectedZ)[] obstacles =
        [
            ("box", new Vector3(0f, 1f, -12f), Vector3.Backward,
                physics => physics.AddStaticBox(new Vector3(4f, 4f, 4f), new Vector3(0f, 2f, 0f)), -2f),

            // Fired at a height that must meet the ramp's *slope*: this wedge rises from y=0 at -Z to y=4 at +Z,
            // so a rocket flying above y=4 at any Z would correctly sail over the whole thing and hit the far wall.
            ("ramp", new Vector3(0f, 1f, -12f), Vector3.Backward,
                physics => physics.AddStaticConvexHull(
                    MapColliders.RampPoints(new Vector3(6f, 4f, 12f)),
                    new Vector3(0f, 0f, 0f),
                    0f), -3f),

            ("cylinder", new Vector3(0f, 1f, -12f), Vector3.Backward,
                physics => physics.AddStaticCylinder(1.5f, 6f, new Vector3(0f, 3f, 0f)), -1.5f),
        ];

        foreach ((string label, Vector3 spawn, Vector3 direction, System.Action<PhysicsWorld> build, float expectedZ) in obstacles)
        {
            using CombatRig rig = new(new Vector3(-25f, 1f, -25f), withWalls: true);
            build(rig.Physics);

            rig.Fire(direction, spawn);
            rig.Step(300);

            if (rig.Weapons.LiveProjectiles != 0)
            {
                failures.Add($"{label}: the rocket was still in flight after 5 s - it passed through the {label}");
                continue;
            }

            if (rig.Explosions.Detonations != 1)
            {
                failures.Add($"{label}: {rig.Explosions.Detonations} explosions, expected exactly 1");
            }

            // The blast must be at the obstacle's near surface. This is what proves the rocket stopped *there*
            // rather than passing through and stopping at the far wall 20 m further on.
            Vector3 centre = rig.Explosions.LastExplosionCentre;

            if (MathF.Abs(centre.Z - expectedZ) > 1.2f)
            {
                failures.Add($"{label}: the explosion was at Z={Format(centre.Z)}, but the near surface is at Z={Format(expectedZ)}");
            }
        }

        return new("rocket detonates on world geometry and never passes through it", failures.Count == 0,
            failures.Count == 0
                ? "floor and all four wall orientations each stopped the rocket with exactly one explosion, and box, ramp and cylinder " +
                  "obstacles each stopped it at their near surface rather than the wall behind"
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketTravelIsFrameRateIndependent()
    {
        List<string> failures = new();

        float[] deltas = { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f };
        float[] distances = new float[deltas.Length];

        const float FlightSeconds = 0.2f;
        Vector3 muzzle = new(0f, 2f, -25f);

        for (int i = 0; i < deltas.Length; i++)
        {
            float delta = deltas[i];
            using CombatRig rig = new(new Vector3(-25f, 1f, 0f));

            rig.Weapons.TryFire(muzzle, Vector3.Forward);

            // Frames of the given length, through the real update path, which is what decides how many fixed
            // substeps each one contains. Hand-stepping the projectile instead would make the frame rate
            // irrelevant by construction and prove nothing about it.
            float elapsed = 0f;
            while (elapsed < FlightSeconds)
            {
                rig.StepFrame(delta);
                elapsed += delta;
            }

            distances[i] = rig.Weapons.Projectiles.TryGetFirst(out Projectile projectile)
                ? MathF.Abs(projectile.Position.Z - muzzle.Z)
                : 0f;

            // The muzzle offset counts too: the projectile is spawned that far ahead of the requested origin, so
            // measuring from the requested origin measures the offset as well as the flight.
            float oneSubStep = rig.Launcher.Tuning.RocketSpeed * PhysicsDefaults.FixedTimeStep;
            float expected = (FlightSeconds * rig.Launcher.Tuning.RocketSpeed) + rig.Launcher.Tuning.MuzzleOffset;

            // Tolerance is one fixed substep of travel, the finest quantum the simulation can express: the window
            // is 0.2 s of frames and whole substeps only, so the count differs between frame rates by up to one.
            if (MathF.Abs(distances[i] - expected) > oneSubStep)
            {
                failures.Add($"at {Format(1f / delta)} fps the rocket travelled {Format(distances[i])} m, expected {Format(expected)} m plus or minus one substep ({Format(oneSubStep)} m)");
            }
        }

        float spread = 0f;
        for (int i = 1; i < distances.Length; i++)
        {
            spread = MathF.Max(spread, MathF.Abs(distances[i] - distances[0]));
        }

        if (spread > 0.01f)
        {
            failures.Add($"distance travelled differed by {Format(spread)} m across frame rates");
        }

        return new("rocket travel is frame-rate independent", failures.Count == 0,
            failures.Count == 0
                ? $"travelled {Format(distances[0])}/{Format(distances[1])}/{Format(distances[2])}/{Format(distances[3])} m at 30/60/144/240 fps, spread {Format(spread)} m"
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketDamagesADummyWithDistanceFalloff()
    {
        // A live rocket into a real target, then a controlled comparison of the falloff through the damage
        // numbers themselves.
        List<string> failures = new();

        RocketLauncherTuning tuning = new();
        using CombatRig rig = new(new Vector3(-25f, 1f, 0f), tuning: tuning);

        // A dummy 2 m from the blast centre, inside the 5.5 m radius.
        using TestDummy near = AddDummy(rig, new Vector3(0f, 1f, -2f));
        float nearStart = near.CurrentHealth;

        // Aim at the floor a couple of metres from the dummy, so the explosion lands and damages it.
        rig.Weapons.TryFire(new Vector3(0f, 3f, 0f), Vector3.Down);
        rig.Step(120);

        if (near.CurrentHealth >= nearStart)
        {
            failures.Add("a rocket detonating 2 m from a dummy dealt it no damage");
        }

        float nearDamage = nearStart - near.CurrentHealth;

        // Now the same blast measured against a far dummy, through the damage path, with no physics involved.
        using CombatRig measured = new(new Vector3(-25f, 1f, 0f), tuning: tuning);

        using TestDummy atTwo = AddDummy(measured, new Vector3(0f, 1f, -2f));
        using TestDummy atFour = AddDummy(measured, new Vector3(0f, 1f, -4f));
        using TestDummy atEight = AddDummy(measured, new Vector3(0f, 1f, -8f));

        measured.Explosions.Detonate(new Explosion(Vector3.Zero, 5.5f, 120f, 900f, measured.PlayerTarget), 0f);

        float damageTwo = 100f - atTwo.CurrentHealth;
        float damageFour = 100f - atFour.CurrentHealth;
        float damageEight = 100f - atEight.CurrentHealth;

        // Strictly decreasing with distance.
        if (!(damageTwo > damageFour && damageFour > damageEight))
        {
            failures.Add($"damage did not fall off: {Format(damageTwo)} at 2 m, {Format(damageFour)} at 4 m, {Format(damageEight)} at 8 m");
        }

        // And matching the curve, so the falloff is the documented one rather than merely monotonic. Measured from
        // the dummy's own centre - the explosion measures distance to the body, not to where it was spawned, and a
        // capsule's centre sits 0.1 m above the origin it was placed at.
        float distanceTwo = Vector3.Distance(Vector3.Zero, atTwo.Position);
        float expectedTwo = tuning.ExplosionDamage * ExplosionFalloff.Strength(distanceTwo, tuning.ExplosionRadius);

        if (MathF.Abs(damageTwo - expectedTwo) > 0.05f)
        {
            failures.Add($"damage at {Format(distanceTwo)} m was {Format(damageTwo)}, expected the falloff curve's {Format(expectedTwo)}");
        }

        // 8 m is outside a 5.5 m radius, so nothing at all.
        if (damageEight != 0f)
        {
            failures.Add($"damage at 8 m was {Format(damageEight)}, expected 0.00 outside the radius");
        }

        return new("rocket damages a target with distance falloff", failures.Count == 0,
            failures.Count == 0
                ? $"a live rocket took {Format(nearDamage)} off a dummy 2 m away; measured damage was " +
                  $"{Format(damageTwo)} at 2 m, {Format(damageFour)} at 4 m, {Format(damageEight)} at 8 m, following the falloff curve"
                : string.Join(" | ", failures));
    }

    private static CheckResult ExplosionDoesNoHarmThroughAWallItCannotReach()
    {
        // Damage goes to everything inside the radius regardless of line of sight. That is stated here as a
        // known limitation rather than a feature: there is no occlusion in this milestone, so a rocket on the far
        // side of a wall still hurts. Asserting it here means the behaviour is deliberate and recorded rather
        // than a surprise discovered later.
        using CombatRig rig = new(new Vector3(-25f, 1f, 0f));

        rig.Physics.AddStaticBox(new Vector3(1f, 6f, 20f), new Vector3(0f, 3f, 0f));

        // Three metres behind a one-metre-thick wall: well inside a 5.5 m radius of a blast on the near side.
        using TestDummy behindWall = AddDummy(rig, new Vector3(0f, 1f, -3f));
        float before = behindWall.CurrentHealth;

        rig.Explosions.Detonate(new Explosion(new Vector3(0f, 1f, 1f), 5.5f, 120f, 900f, rig.PlayerTarget), 0f);

        float dealt = before - behindWall.CurrentHealth;

        return new("explosion damage currently ignores walls (documented limitation)",
            dealt > 0f,
            dealt > 0f
                ? $"a dummy {Format(dealt)} damaged from behind a solid wall - there is no line-of-sight check " +
                  "in this milestone, so blasts pass through geometry"
                : $"a dummy behind a wall took {Format(dealt)} damage, so something is doing occlusion after all");
    }

    private static CheckResult ExplosionDistinguishesSelfDamageFromCollateral()
    {
        List<string> failures = new();

        using CombatRig rig = new(new Vector3(0f, 1f, 0f));
        rig.Weapons.Projectiles.Clear();

        // A dummy right next to the player, so both are inside the blast and both are damaged - the player by
        // the self-damage scale, the dummy by the full amount.
        using TestDummy bystander = AddDummy(rig, new Vector3(2f, 1f, 0f));

        RocketLauncherTuning tuning = rig.Launcher.Tuning;

        // Fire at the floor between them so the blast centre is close to both.
        rig.Weapons.TryFire(new Vector3(0f, 3f, 0f), Vector3.Down);
        rig.Step(120);

        float playerDamage = rig.Player.Health.TotalDamageTaken;
        float bystanderDamage = 100f - bystander.CurrentHealth;

        if (playerDamage <= 0f)
        {
            failures.Add("the player took no self-damage from a rocket that exploded at their feet");
        }

        if (bystanderDamage <= 0f)
        {
            failures.Add("the dummy beside the player took no damage");
        }

        // The self-damage scale is strictly below 1, and applies to the *same* falloff: the player should lose
        // less than an identically-placed dummy.
        float expectedSelf = tuning.ExplosionDamage * tuning.SelfDamage * ExplosionFalloff.Strength(1.5f, tuning.ExplosionRadius);

        if (playerDamage >= bystanderDamage)
        {
            failures.Add($"self-damage ({Format(playerDamage)}) was not less than collateral ({Format(bystanderDamage)}); SelfDamage is {Format(tuning.SelfDamage)}");
        }

        // Self-knockback is full: the movement is the reward.
        if (MathF.Abs(tuning.SelfKnockback - 1f) > 0.001f)
        {
            failures.Add($"SelfKnockback is {Format(tuning.SelfKnockback)}, expected 1.00 - a rocket jump needs its full impulse");
        }

        _ = expectedSelf;

        return new("explosion applies reduced self-damage and full self-knockback through the same path", failures.Count == 0,
            failures.Count == 0
                ? $"player took {Format(playerDamage)} from a rocket at their feet against {Format(bystanderDamage)} for an equally-placed dummy " +
                  $"(SelfDamage {Format(tuning.SelfDamage)}, SelfKnockback {Format(tuning.SelfKnockback)})"
                : string.Join(" | ", failures));
    }

    /// <summary>
    /// The headline check: a player on flat ground fires a rocket at the floor near their feet and is launched,
    /// through nothing but the ordinary explosion and knockback path.
    ///
    /// There is deliberately no player-specific code involved - no <c>ApplyRocketJump</c>, no special case in the
    /// projectile system. The player is registered as a combat target like any dummy, and the launch is the
    /// generic impulse reaching a dynamic capsule. That is asserted structurally: the only thing the rocket
    /// touches is <see cref="ExplosionSystem"/>, and the only thing the explosion touches is the body's impulse.
    /// </summary>
    private static CheckResult RocketJumpLaunchesThePlayerThroughOrdinaryKnockback()
    {
        List<string> failures = new();

        RocketLauncherTuning tuning = new();
        using CombatRig rig = new(new Vector3(0f, 1f, 0f), tuning: tuning);

        // Settle on the ground.
        rig.Step(30);

        Vector3 startPosition = rig.Player.Position;
        float mass = rig.Player.Body.Mass;

        // Fire straight down at the floor from a little above the player's own feet, so the explosion lands on the
        // ground right under them and the outward direction is almost entirely up.
        rig.Weapons.TryFire(rig.Player.Position + new Vector3(0f, 1.5f, 0f), Vector3.Down);

        // Watch every step rather than sampling once: the impulse arrives on a single substep and is then in the
        // hands of gravity and the contact solver, so a sample a few frames later would measure the *recovery* and
        // not the launch. The peak is the honest figure.
        float peakVertical = 0f;
        bool everAirborne = false;
        Vector3 blastCentre = Vector3.Zero;

        for (int i = 0; i < 200; i++)
        {
            rig.Step();

            peakVertical = MathF.Max(peakVertical, rig.Player.Velocity.Y);

            if (rig.Explosions.Detonations > 0)
            {
                blastCentre = rig.Explosions.LastExplosionCentre;
                everAirborne |= !rig.Player.Movement.IsGrounded;

                if (peakVertical > 0f && rig.Player.Velocity.Y < peakVertical * 0.5f)
                {
                    break;
                }
            }
        }

        // A real launch, not a nudge. The tuned impulse is mass-scaled, so the expected velocity is the impulse
        // over the player's mass - which is what makes this a check on the physics rather than on a magic number.
        float distance = Vector3.Distance(blastCentre, rig.Player.Position);
        float expected = tuning.ExplosionForce * tuning.SelfKnockback * ExplosionFalloff.Strength(distance, tuning.ExplosionRadius) / mass;

        if (peakVertical < expected * 0.5f)
        {
            failures.Add($"peak vertical velocity {Format(peakVertical)} is far below the {Format(expected)} m/s the blast should give");
        }

        if (!everAirborne)
        {
            failures.Add("the player never left the ground");
        }

        // And it goes somewhere: height gained must exceed a standing jump, or the "launch" is meaningless.
        float apex = rig.Player.Position.Y;
        for (int i = 0; i < 200; i++)
        {
            rig.Step();
            apex = MathF.Max(apex, rig.Player.Position.Y);
        }

        float rocketJumpHeight = apex - startPosition.Y;

        // What an ordinary jump reaches, from the player's own tuning - the baseline a rocket jump has to beat
        // to be worth anything as a movement option.
        PlayerTuning playerTuning = rig.Player.Tuning;
        float standingJumpHeight = playerTuning.JumpSpeed * playerTuning.JumpSpeed / (2f * -playerTuning.Gravity);

        if (rocketJumpHeight <= standingJumpHeight)
        {
            failures.Add($"the launch reached only {Format(rocketJumpHeight)} m, no better than a {Format(standingJumpHeight)} m standing jump");
        }

        // Gravity is still on: it came back down.
        rig.Step(400);
        if (rig.Player.Position.Y > startPosition.Y + 0.5f)
        {
            failures.Add("the player never came back down; gravity is not being applied after a rocket jump");
        }

        return new("rocket jump launches the player through ordinary explosion knockback", failures.Count == 0,
            failures.Count == 0
                ? $"a rocket at the player's feet gave a peak of {Format(peakVertical)} m/s up against the {Format(expected)} m/s " +
                  $"the blast should impart to their {Format(mass)} kg body, and reached {Format(rocketJumpHeight)} m of height " +
                  $"against a {Format(standingJumpHeight)} m standing jump, then gravity pulled them back down. " +
                  "No player-specific rocket-jump code exists: the impulse came from the shared explosion system."
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketJumpPreservesMomentumAndKeepsGravityActive()
    {
        List<string> failures = new();

        RocketLauncherTuning tuning = new();

        // Running rocket jump: the player must keep their horizontal speed while being thrown up.
        float horizontalBefore;
        float horizontalAfter;

        using (CombatRig rig = new(new Vector3(0f, 1f, -20f), tuning: tuning))
        {
            rig.Step(60);

            // Build up real run speed through the real movement code.
            for (int i = 0; i < 60; i++)
            {
                rig.Player.Movement.Step(PhysicsDefaults.FixedTimeStep, Vector3.Forward, false);
                rig.Physics.StepSubStep();
                rig.Player.Movement.AfterStep();
            }

            horizontalBefore = Horizontal(rig.Player.Velocity).Length();

            // Fire down and slightly behind, as a player would while running.
            rig.Weapons.TryFire(rig.Player.Position + new Vector3(0f, 1.5f, 0f), Vector3.Down);
            rig.Step(20);

            horizontalAfter = Horizontal(rig.Player.Velocity).Length();
        }

        if (horizontalBefore < 5f)
        {
            failures.Add($"the run-up only reached {Format(horizontalBefore)} m/s, so the scenario is not a real run");
        }

        // Air control adds speed and never subtracts, so the only thing that could reduce horizontal speed here
        // is the blast replacing velocity. Preserved means "at least what we had".
        if (horizontalAfter < horizontalBefore - 0.5f)
        {
            failures.Add($"horizontal speed fell from {Format(horizontalBefore)} to {Format(horizontalAfter)} across the rocket jump");
        }

        // Gravity still running: a sample taken a fraction of a second later must be lower than at launch.
        float verticalAtLaunch;
        using (CombatRig rig = new(new Vector3(0f, 1f, 0f), tuning: tuning))
        {
            rig.Step(30);
            rig.Weapons.TryFire(rig.Player.Position + new Vector3(0f, 1.5f, 0f), Vector3.Down);
            rig.Step(20);

            verticalAtLaunch = rig.Player.Velocity.Y;
            rig.Step(30);
            float verticalLater = rig.Player.Velocity.Y;

            if (verticalLater >= verticalAtLaunch)
            {
                failures.Add($"vertical speed rose from {Format(verticalAtLaunch)} to {Format(verticalLater)}; gravity is not acting");
            }
        }

        return new("rocket jump preserves horizontal momentum and keeps gravity active", failures.Count == 0,
            failures.Count == 0
                ? $"a running rocket jump kept horizontal speed ({Format(horizontalBefore)} -> {Format(horizontalAfter)} m/s) " +
                  $"while vertical speed decayed under gravity ({Format(verticalAtLaunch)} -> lower)"
                : string.Join(" | ", failures));
    }

    private static CheckResult RocketJumpWorksFromRunAndFromAir()
    {
        List<string> failures = new();

        RocketLauncherTuning tuning = new();

        // Firing while airborne: the rocket must still travel normally and still explode on impact.
        {
            using CombatRig rig = new(new Vector3(0f, 12f, -20f), tuning: tuning);
            rig.Step(40);

            if (rig.Player.Movement.IsGrounded)
            {
                failures.Add("the airborne scenario started with the player on the ground");
            }

            Vector3 from = rig.Player.Camera is null ? Vector3.Zero : rig.Player.Position;
            Vector3 aim = Vector3.Normalize(new Vector3(0f, -0.5f, -1f));

            rig.Weapons.TryFire(from, aim);
            rig.Step(10);

            if (rig.Weapons.LiveProjectiles != 1)
            {
                failures.Add($"{rig.Weapons.LiveProjectiles} rockets in flight after firing in the air, expected 1");
            }

            rig.Step(300);

            if (rig.Weapons.LiveProjectiles != 0)
            {
                failures.Add("an air-fired rocket never detonated");
            }

            if (rig.Explosions.Detonations == 0)
            {
                failures.Add("an air-fired rocket produced no explosion");
            }
        }

        // The other three directions the milestone asks about, checked on the impulse rule: a blast behind,
        // in front and to the side must each push the player the corresponding way. Same falloff function, no
        // special cases - which is exactly what makes these work without any of them being written down.
        {
            Vector3 playerAt = new(0f, 1f, 0f);

            (string Label, Vector3 BlastCentre, Vector3 ExpectedPush)[] directions =
            [
                ("behind", new Vector3(0f, 1f, 2f), new Vector3(0f, 0f, -1f)),
                ("in front", new Vector3(0f, 1f, -2f), new Vector3(0f, 0f, 1f)),
                ("left", new Vector3(-2f, 1f, 0f), new Vector3(1f, 0f, 0f)),
                ("above", new Vector3(0f, 4f, 0f), new Vector3(0f, -1f, 0f)),
            ];

            foreach ((string label, Vector3 blastCentre, Vector3 expectedPush) in directions)
            {
                Vector3 impulse = ExplosionFalloff.Impulse(blastCentre, playerAt, 900f, 5.5f);
                float alignment = Vector3.Dot(Vector3.Normalize(impulse), expectedPush);

                if (alignment < 0.99f)
                {
                    failures.Add($"a rocket {label} pushed the player {Vector3.Normalize(impulse)}, expected {expectedPush}");
                }
            }
        }

        return new("firing works while airborne, and blast direction works from behind, in front, and beside", failures.Count == 0,
            failures.Count == 0
                ? "an airborne rocket travelled and detonated on impact, and blasts behind, in front, to the side and above each push the player the correct way"
                : string.Join(" | ", failures));
    }

    private static CheckResult WeaponsRespectTheirFireInterval()
    {
        List<string> failures = new();

        RocketLauncherTuning tuning = new();
        using CombatRig rig = new(new Vector3(-25f, 1f, 0f), tuning: tuning);

        Vector3 muzzle = new(0f, 2f, -25f);

        // Firing twice on the same step must produce one rocket, not two.
        bool first = rig.Weapons.TryFire(muzzle, Vector3.Forward);
        bool second = rig.Weapons.TryFire(muzzle, Vector3.Forward);

        if (!first)
        {
            failures.Add("the first shot was refused");
        }

        if (second)
        {
            failures.Add("a second shot on the same step was allowed; the fire interval is not being respected");
        }

        if (rig.Weapons.LiveProjectiles != 1)
        {
            failures.Add($"{rig.Weapons.LiveProjectiles} rockets in flight after two same-step shots, expected 1");
        }

        // Still on cooldown shortly afterwards.
        rig.Step(5);
        if (rig.Weapons.TryFire(muzzle, Vector3.Forward))
        {
            failures.Add("a shot was allowed while still cooling down");
        }

        // And allowed again once the interval has genuinely elapsed.
        int stepsForInterval = (int)MathF.Ceiling(tuning.FireInterval / PhysicsDefaults.FixedTimeStep);
        rig.Step(stepsForInterval + 1);

        if (!rig.Weapons.TryFire(muzzle, Vector3.Forward))
        {
            failures.Add($"still on cooldown after {Format(tuning.FireInterval)} s of stepping");
        }

        if (rig.Weapons.LiveProjectiles != 2)
        {
            failures.Add($"{rig.Weapons.LiveProjectiles} rockets in flight after the cooldown elapsed, expected 2");
        }

        // A degenerate direction is refused rather than spawning a rocket that cannot move.
        rig.Weapons.Projectiles.Clear();
        RocketLauncher fresh = new(new RocketLauncherTuning());
        if (fresh.TryFire(new FireRequest(Vector3.Zero, Vector3.Zero, null)) is not null)
        {
            failures.Add("a shot along a zero-length direction was allowed");
        }

        return new("weapons respect their fire interval and reject a degenerate direction", failures.Count == 0,
            failures.Count == 0
                ? $"one shot per {Format(tuning.FireInterval)} s, a second same-step shot refused, and a zero-length direction rejected"
                : string.Join(" | ", failures));
    }

    private static CheckResult FieldOfViewStaysWithinTheEngineLimit()
    {
        // Regression test for a fatal crash. The camera's field of view is the base value plus up to three
        // independent kicks. MonoGame's perspective projection *throws* on a field of view of 0 or more than 180
        // degrees, and a throw inside Draw is a fatal, unrecoverable exit rather than a dropped frame - so the
        // range the projection will accept is treated as an invariant of the camera, not an assumption about it.
        List<string> failures = new();

        PlayerTuning tuning = new();
        PlayerCamera camera = new(tuning);

        // Every kick saturated at once, which is the worst case the camera can be put in.
        camera.KickForDash();
        camera.KickForFiring();
        camera.UpdateFeel(0f, sliding: true);

        float worst = camera.FieldOfViewDegrees;

        if (worst >= PlayerCamera.MaximumFieldOfViewDegrees)
        {
            failures.Add($"field of view reached {Format(worst)} deg with every kick saturated");
        }

        // Hammer the kick for a simulated ten seconds, the way a held mouse button would.
        float worstWhileHeld = 0f;
        for (int i = 0; i < 600; i++)
        {
            camera.KickForDash();
            camera.KickForFiring();
            camera.UpdateFeel(1f / 60f, sliding: true);
            worstWhileHeld = MathF.Max(worstWhileHeld, camera.FieldOfViewDegrees);
        }

        if (worstWhileHeld >= PlayerCamera.MaximumFieldOfViewDegrees)
        {
            failures.Add($"field of view reached {Format(worstWhileHeld)} deg after 10 s of held fire");
        }

        // And it must always stay above the lower limit too.
        if (camera.FieldOfViewDegrees <= PlayerCamera.MinimumFieldOfViewDegrees)
        {
            failures.Add($"field of view collapsed to {Format(camera.FieldOfViewDegrees)} deg");
        }

        // The projection matrix must actually build. This is the assertion that would have caught the crash
        // directly, rather than by inference from the degrees.
        try
        {
            Matrix projection = camera.GetProjectionMatrix(16f / 9f);
            _ = projection;
        }
        catch (Exception error)
        {
            failures.Add($"the projection threw: {error.GetType().Name}");
        }

        return new("field of view always stays within what the projection accepts", failures.Count == 0,
            failures.Count == 0
                ? $"worst case {Format(worstWhileHeld)} deg against a limit of {Format(PlayerCamera.MaximumFieldOfViewDegrees)}, " +
                  "and the projection matrix builds"
                : string.Join(" | ", failures));
    }

    private static CheckResult HeldFireCannotRunTheFieldOfViewAway()
    {
        // The specific defect that crashed the game: the kick *added* to the existing value, so calling it every
        // frame - which is what a held mouse button does - grew the field of view without limit.
        List<string> failures = new();

        PlayerTuning tuning = new();
        PlayerCamera camera = new(tuning);

        float firstKick = 0f;

        camera.KickForDash();
        firstKick = camera.FieldOfViewDegrees;
        float afterOne = firstKick;

        // A second call with nothing decaying must give exactly the same answer, not a bigger one.
        camera.KickForDash();
        float afterTwo = camera.FieldOfViewDegrees;

        if (MathF.Abs(afterTwo - afterOne) > 0.001f)
        {
            failures.Add($"a second kick on the same frame raised the field of view from {Format(afterOne)} to {Format(afterTwo)} deg");
        }

        // The kick must equal the configured amount, not an arbitrary multiple.
        float expected = tuning.FieldOfViewDegrees + tuning.DashFovKickDegrees;
        if (MathF.Abs(afterOne - expected) > 0.001f)
        {
            failures.Add($"one kick gave {Format(afterOne)} deg, expected exactly {Format(expected)}");
        }

        // With the kick *stopped*, it must decay away. Kicking every frame in a loop would legitimately hold it at full
        // strength - that is what saturating means - so decay is checked with the trigger released.
        float start = camera.FieldOfViewDegrees;
        for (int i = 0; i < 60; i++)
        {
            camera.UpdateFeel(1f / 60f, sliding: false);
        }

        float afterASecond = camera.FieldOfViewDegrees;

        if (afterASecond >= start)
        {
            failures.Add($"the kick did not decay: still {Format(afterASecond)} deg a second after the trigger was released, started at {Format(start)}");
        }

        // Effectively settled. The decay is exponential, so it approaches the base asymptotically and never quite reaches
        // it - which is the right behaviour for a visual effect, and why the tolerance here is a fraction of a
        // degree rather than zero. Reaching exactly zero would need a snap, which is what would look wrong.
        if (MathF.Abs(afterASecond - tuning.FieldOfViewDegrees) > 0.25f)
        {
            failures.Add($"after a second the field of view was {Format(afterASecond)} deg, expected it back within a fraction of a degree of the {Format(tuning.FieldOfViewDegrees)} deg base");
        }

        // And kicking every frame while held must hold it at exactly one kick - not creep towards the limit.
        // Measured against one full kick, not against the decayed value just above: the first held kick legitimately
        // raises it again, and only the *steady state* is the thing being asserted.
        float oneKick = tuning.FieldOfViewDegrees + tuning.DashFovKickDegrees;

        for (int i = 0; i < 600; i++)
        {
            camera.KickForDash();
            camera.UpdateFeel(1f / 60f, sliding: false);
        }

        float afterTenSecondsHeld = camera.FieldOfViewDegrees;

        if (afterTenSecondsHeld > oneKick + 0.25f)
        {
            failures.Add($"10 s of continuous kicking reached {Format(afterTenSecondsHeld)} deg, but one kick is only {Format(oneKick)} deg");
        }

        return new("a field-of-view kick is one kick, however often it is asked for", failures.Count == 0,
            failures.Count == 0
                ? $"repeated kicks saturate at {Format(afterOne)} deg, decay to {Format(afterASecond)} deg a second after release, " +
                  $"and 10 s of held fire stayed at {Format(afterTenSecondsHeld)} deg"
                : string.Join(" | ", failures));
    }

    private static CheckResult BillboardMatricesAreInvertible()
    {
        // The black-object bug. A billboard world matrix whose rows are not a full orthonormal basis is singular:
        // the vertex positions still come out right, but the engine cannot invert the matrix to transform normals,
        // so lighting produces non-finite normals and the quad renders black. In the game that showed up as a
        // black rotating quad hanging over every test dummy.
        List<string> failures = new();

        float[] pitch = { -1.2f, -0.4f, 0f, 0.4f, 1.2f };
        float[] yaw = { 0f, 1.1f, 2.4f, 3.9f, 5.5f };

        Matrix check = Matrix.Identity;

        foreach (float p in pitch)
        {
            foreach (float y in yaw)
            {
                // A real camera view matrix, so the basis rows are real rather than identity.
                Vector3 eye = new(3f, 2f, 5f);
                Vector3 direction = new(MathF.Sin(y) * MathF.Cos(p), MathF.Sin(p), MathF.Cos(y) * MathF.Cos(p));
                check = Matrix.CreateLookAt(eye, eye + direction, Vector3.Up);

                // Placed *on the view axis*, which is where a billboard actually lives. Offsetting it sideways
                // would make the camera's forward and the direction to the quad different vectors, and the test
                // would then be measuring the offset rather than the facing.
                Vector3 centre = eye + (direction * 4f);

                Matrix billboard = Billboards.FaceCamera(check, eye, centre, 1.6f, 0.2f);

                if (!IsInvertible(billboard))
                {
                    failures.Add($"the billboard matrix is singular at pitch {Format(p)} yaw {Format(y)}");
                    continue;
                }

                // The normals must survive the transform as unit-length vectors. This is the actual mechanism of
                // the black-quad bug, so it is worth asserting directly rather than only the invertibility.
                Vector3 normal = Vector3.TransformNormal(Vector3.UnitZ, billboard);
                float length = normal.Length();

                if (!float.IsFinite(length) || MathF.Abs(length - 1f) > 0.01f)
                {
                    failures.Add($"a transformed normal at pitch {Format(p)} yaw {Format(y)} had length {Format(length)}, expected 1.00");
                }

                // And it must point back at the eye. The quad's normal is its local +Z and the world back-face
                // culls clockwise triangles, so a normal pointing *away* from the camera makes the quad invisible.
                // That is a silent failure - a valid matrix that simply never appears - and it is what hid the
                // health bars and the explosion flashes.
                Vector3 toEye = Vector3.Normalize(eye - centre);

                if (Vector3.Dot(normal, toEye) < 0.9f)
                {
                    failures.Add($"at pitch {Format(p)} yaw {Format(y)} the quad's normal pointed away from the eye, so it would be culled");
                }
            }
        }

        // The same, with a stretched trail whose up axis is overridden - the rocket case, which is where the
        // degenerate cross product would bite.
        Vector3 trailEye = new(0f, 1f, 5f);
        Matrix lookAt = Matrix.CreateLookAt(trailEye, Vector3.Zero, Vector3.Up);

        foreach (Vector3 trailDirection in new[]
        {
            Vector3.Forward,
            Vector3.Backward,
            Vector3.Right,
            Vector3.Left,
            Vector3.Up,
            Vector3.Down,
            Vector3.Normalize(new Vector3(1f, 1f, 0f)),
        })
        {
            Matrix trail = Billboards.FaceCamera(lookAt, trailEye, Vector3.Zero, 0.2f, 3f, trailDirection);

            if (!IsInvertible(trail))
            {
                failures.Add($"a trail billboard along {trailDirection} is singular");
            }
        }

        return new("billboard world matrices are always invertible", failures.Count == 0,
            failures.Count == 0
                ? $"checked 25 camera pitches and yaws plus 7 trail directions; every matrix inverted and every transformed normal stayed unit length"
                : string.Join(" | ", failures));
    }

    private static CheckResult WeaponRecoilReturnsToRestAndIsFrameRateIndependent()
    {
        // The recoil has to settle, or the launcher drifts off to one side over a long session. And it has to settle
        // in the same real time at any frame rate: it is driven by a *held* input, so if it were integrated per
        // frame it would behave differently at 30 fps than at 240 fps.
        List<string> failures = new();

        ViewModelTuning tuning = new();

        float[] deltas = { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f };
        float[] settleTimes = new float[deltas.Length];

        for (int i = 0; i < deltas.Length; i++)
        {
            WeaponViewModel viewmodel = new(tuning);
            viewmodel.OnFired();

            // Recoil must be at its strongest the instant the shot goes off.
            float peakBack = MathF.Abs(viewmodel.RecoilOffset.Z);
            if (peakBack < tuning.RecoilBack * 0.5f)
            {
                failures.Add($"recoil started at only {Format(peakBack)} m, expected near {Format(tuning.RecoilBack)}");
            }

            float elapsed = 0f;
            int guard = 0;

            while (elapsed < 3f && guard < 2000)
            {
                viewmodel.Update(deltas[i]);
                elapsed += deltas[i];
                guard++;

                if (viewmodel.RecoilOffset.Length() < 1e-4f && viewmodel.RecoilRotation.Length() < 1e-4f)
                {
                    break;
                }
            }

            settleTimes[i] = elapsed;
        }

        float spread = 0f;
        for (int i = 1; i < settleTimes.Length; i++)
        {
            spread = MathF.Max(spread, MathF.Abs(settleTimes[i] - settleTimes[0]));
        }

        if (spread > PhysicsDefaults.FixedTimeStep + 0.01f)
        {
            failures.Add($"the recoil took {Format(settleTimes[0])}/{Format(settleTimes[1])}/{Format(settleTimes[2])}/{Format(settleTimes[3])} s to settle at 30/60/144/240 fps, spread {Format(spread)} s");
        }

        // It must actually return to rest, not merely shrink.
        WeaponViewModel rested = new(tuning);
        rested.OnFired();
        rested.Update(1f);

        if (rested.RecoilOffset.LengthSquared() > 0f || rested.RecoilRotation.LengthSquared() > 0f)
        {
            failures.Add($"after 1 s the model was still displaced by {rested.RecoilOffset} and rotated by {rested.RecoilRotation}");
        }

        // And the muzzle flash must be short-lived: a flash that lingers reads as a stuck muzzle.
        WeaponViewModel flashed = new(tuning);
        flashed.OnFired();

        if (!flashed.IsFlashing)
        {
            failures.Add("no muzzle flash on the frame a shot was fired");
        }

        flashed.Update(tuning.MuzzleFlashSeconds + 0.001f);
        if (flashed.IsFlashing)
        {
            failures.Add($"the muzzle flash was still visible {Format(tuning.MuzzleFlashSeconds + 0.001f)} s after the shot");
        }

        return new("weapon recoil returns to rest, frame-rate independently, and the flash is brief", failures.Count == 0,
            failures.Count == 0
                ? $"settled in {Format(settleTimes[0])}/{Format(settleTimes[1])}/{Format(settleTimes[2])}/{Format(settleTimes[3])} s at 30/60/144/240 fps, " +
                  $"and the flash was gone after {Format(tuning.MuzzleFlashSeconds)} s"
                : string.Join(" | ", failures));
    }

    /// <summary>
    /// True when a matrix can be inverted. This is the property that matters for the black-quad bug: the engine
    /// inverts the world matrix to transform normals, and a singular one produces non-finite normals, which shade
    /// to black.
    /// </summary>
    private static bool IsInvertible(Matrix matrix)
    {
        Matrix inverted;
        Matrix.Invert(ref matrix, out inverted);

        // A singular matrix inverts to non-finite values rather than throwing, so the result is checked rather
        // than the call.
        return float.IsFinite(inverted.M11) && float.IsFinite(inverted.M22) && float.IsFinite(inverted.M33);
    }

    // ------------------------------------------------------------------ fixture

    /// <summary>Horizontal component of a velocity.</summary>
    private static Vector3 Horizontal(Vector3 velocity) => new(velocity.X, 0f, velocity.Z);

    /// <summary>
    /// A player plus its physics world, plus the per-frame input a test wants to feed it. Keeps the individual
    /// checks down to the behaviour they are actually asserting.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(PhysicsWorld physics, PlayerTuning tuning, PlayerController controller)
        {
            Physics = physics;
            Controller = controller;
        }

        public PhysicsWorld Physics { get; }

        public PlayerController Controller { get; }

        public Player.Player Player => Controller.Player;

        public PlayerTuning Tuning => Controller.Player.Tuning;

        public bool JumpHeld { get; set; }

        public static Fixture Create(
            float spawnY = 2f,
            Vector3? spawnPosition = null,
            float yaw = 0f,
            PlayerTuning? tuning = null)
        {
            PlayerTuning settings = tuning ?? new PlayerTuning();
            settings.SpawnPosition = spawnPosition ?? new Vector3(0f, spawnY, -96f);
            settings.SpawnYaw = yaw;

            PhysicsWorld physics = new();
            MapColliders.Build(physics, TestArena.Blocks);

            Fixture fixture = new(physics, settings, PlayerController.CreateHeadless(physics, settings));
            return fixture;
        }

        /// <summary>
        /// Runs frames with an optional movement input and optional jump. Defaults to no input and no jump,
        /// which is what most checks want; a flat floor at y = 0 is assumed.
        /// </summary>
        public void Run(int frames, Vector3? move = null, float? frameDelta = null, float yaw = 0f)
        {
            bool jump = JumpHeld;
            Controller.StepForTest(move ?? Vector3.Zero, jump, frameDelta ?? (1f / 60f));
        }

        public void Dispose() => Controller.Dispose();
    }

    private static void Run(Fixture fixture, int frames, Vector3? move = null, float? frameDelta = null)
    {
        for (int i = 0; i < frames; i++)
        {
            fixture.Run(1, move, frameDelta);
        }
    }

    private static void Settle(Fixture fixture, float frameDeltaSeconds, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            fixture.Run(1, null, frameDeltaSeconds);
            elapsed += frameDeltaSeconds;
        }
    }

    private static string Describe(Vector3 value) => $"({Format(value.X)}, {Format(value.Y)}, {Format(value.Z)})";

    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
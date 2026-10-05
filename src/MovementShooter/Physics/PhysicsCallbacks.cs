using System;
using BepuPhysics;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;

namespace MovementShooter.Physics;

/// <summary>
/// BEPU callback hooks. We want default behaviour (generate every contact, no engine-side friction) so
/// gameplay can integrate friction and stick-to-ground itself, which keeps movement predictable.
/// </summary>
internal readonly struct DefaultNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    private static readonly PairMaterialProperties FrictionlessMaterial = new()
    {
        FrictionCoefficient = 0f,
        MaximumRecoveryVelocity = PhysicsDefaults.MaximumRecoveryVelocity,
        SpringSettings = new SpringSettings(PhysicsDefaults.ContactSpringFrequency, 1f),
    };

    public void Initialize(Simulation simulation)
    {
    }

    public bool AllowContactGeneration(int workerIndex, BepuPhysics.Collidables.CollidableReference a, BepuPhysics.Collidables.CollidableReference b, ref float speculativeMargin) => true;

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;

    public bool ConfigureContactManifold<TManifold>(
        int workerIndex,
        CollidablePair pair,
        ref TManifold manifold,
        out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial = FrictionlessMaterial;
        return true;
    }

    public bool ConfigureContactManifold(
        int workerIndex,
        CollidablePair pair,
        int childIndexA,
        int childIndexB,
        ref ConvexContactManifold manifold) => true;

    public void Dispose()
    {
    }
}

/// <summary>
/// BEPU pose integrator hooks. We integrate velocity ourselves (gravity, acceleration), so the callbacks
/// deliberately do nothing.
/// </summary>
internal readonly struct DefaultPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public AngularIntegrationMode AngularIntegrationMode => default;

    public bool AllowSubstepsForUnconstrainedBodies => false;

    public bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
    }

    public void IntegrateVelocity(
        System.Numerics.Vector<int> bodyIndices,
        BepuUtilities.Vector3Wide position,
        BepuUtilities.QuaternionWide orientation,
        BodyInertiaWide localInertia,
        System.Numerics.Vector<int> integrationMask,
        int workerIndex,
        System.Numerics.Vector<float> dt,
        ref BodyVelocityWide velocity)
    {
    }
}
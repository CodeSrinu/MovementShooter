using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities.Memory;
using Microsoft.Xna.Framework;

namespace MovementShooter.Physics;

/// <summary>Opaque handle to a collidable in the physics world, so gameplay never sees engine types.</summary>
public readonly record struct CollidableId(uint Packed)
{
    public static CollidableId None => default;

    public bool Exists => Packed != 0;
}

/// <summary>Primitive types the physics world contains.</summary>
public enum ColliderKind
{
    Box,
    Cylinder,
    Capsule,
    ConvexHull,
}

    /// <summary>
/// A collider as the physics world actually placed it. Recorded when the collider is created, using the
    /// same pose handed to the engine, so debug rendering and geometry audits see exactly what BEPU sees.
    /// <para>
    /// <see cref="HullPoints"/> is the exception: convex hull vertices are stored in world space, because the
    /// engine's recentred pose is not the block's pose and re-deriving them would be a second chance to get
    /// the transform wrong.
    /// </para>
    /// </summary>
public sealed record ColliderShape(
    ColliderKind Kind,
    Matrix Transform,
    BoundingBox WorldBounds,
    Vector3 BoxSize,
    float Radius,
    float Length,
    IReadOnlyList<Vector3> HullPoints,
    bool IsPlayer)
{
    /// <summary>Short label for diagnostics.</summary>
    public string Describe() => Kind switch
    {
        ColliderKind.Box => $"Box {BoxSize.X:0.#}x{BoxSize.Y:0.#}x{BoxSize.Z:0.#}",
        ColliderKind.Cylinder => $"Cylinder r{Radius:0.#} h{Length:0.#}",
        ColliderKind.Capsule => $"Capsule r{Radius:0.#} len{Length:0.#}",
        _ => $"ConvexHull {HullPoints.Count} pts",
    };
}

/// <summary>Result of a physics ray query.</summary>
public readonly record struct RayHit(bool Hit, Vector3 Position, Vector3 Normal, float Distance, CollidableId Collidable);

/// <summary>
/// Wraps the BEPUv2 simulation. This is the only class in the game that knows the physics engine exists:
/// gameplay asks for bodies, shapes, fixed steps and ray queries using MonoGame types.
/// </summary>
public sealed class PhysicsWorld : IDisposable
{
    /// <summary>Slack allowed when deciding whether the accumulator has reached a whole step.</summary>
    private const float StepTolerance = 1e-4f;

    private readonly Simulation _simulation;
    private readonly Dictionary<BepuPhysics.BodyHandle, PhysicsBody> _bodies = new();
    private readonly List<ColliderShape> _shapes = new();

    /// <summary>
    /// Callbacks invoked once per fixed step, before the solver runs.
    ///
    /// This is how anything that must advance in lockstep with the simulation - projectiles, explosion
    /// bookkeeping - gets stepped without threading a substep loop through every caller. The callback runs
    /// <i>before</i> the timestep so that anything it writes to velocity is integrated by the step that
    /// follows, rather than sitting unapplied for a frame.
    /// </summary>
    private readonly List<Action> _preStepCallbacks = new();

    /// <summary>Packed ids of collidables that belong to dynamic bodies, for <see cref="IsDynamic"/>.</summary>
    private readonly HashSet<uint> _dynamicCollidables = new();

    private float _accumulator;
    private bool _disposed;

    public PhysicsWorld()
    {
        _simulation = Simulation.Create(
            new BufferPool(),
            new DefaultNarrowPhaseCallbacks(),
            new DefaultPoseIntegratorCallbacks(),
            new SolveDescription(PhysicsDefaults.SolverVelocityIterations, PhysicsDefaults.SolverSubStepCount),
            new DefaultTimestepper(),
            initialAllocationSizes: null);
    }


    public int StaticCount => _simulation.Statics.Count;

    /// <summary>
    /// Every collider this world created, in creation order, with the exact world transform handed to the
    /// engine. Used by the collision debug view and by the render/collision equivalence audit.
    /// </summary>
    public IReadOnlyList<ColliderShape> Shapes => _shapes;

    // ----------------------------------------------------------------- statics

    /// <summary>Adds a box collider. <paramref name="size"/> is the full extent on each axis.</summary>
    public void AddStaticBox(Vector3 size, Vector3 position, float yawDegrees = 0f)
    {
        Matrix transform = PoseOf(position, yawDegrees);
        BepuPhysics.Collidables.Box box = new(size.X, size.Y, size.Z);
        TypedIndex index = _simulation.Shapes.Add(box);
        _simulation.Statics.Add(new StaticDescription(ToRigidPose(transform), index));

        _shapes.Add(new ColliderShape(
            ColliderKind.Box,
            transform,
            WorldBoundsOf(size),
            size,
            0f,
            0f,
            Array.Empty<Vector3>(),
            false));
    }

    /// <summary>Adds a Y-axis aligned cylinder. <paramref name="height"/> is the full height.</summary>
    public void AddStaticCylinder(float radius, float height, Vector3 position)
    {
        Matrix transform = PoseOf(position, 0f);
        Cylinder cylinder = new(radius, height);
        TypedIndex index = _simulation.Shapes.Add(cylinder);
        _simulation.Statics.Add(new StaticDescription(ToRigidPose(transform), index));

        _shapes.Add(new ColliderShape(
            ColliderKind.Cylinder,
            transform,
            WorldBoundsOf(new Vector3(radius * 2f, height, radius * 2f)),
            Vector3.Zero,
            radius,
            height,
            Array.Empty<Vector3>(),
            false));
    }

    /// <summary>Adds a convex hull from local-space points, for shapes with no engine primitive (ramps).</summary>
    public void AddStaticConvexHull(IReadOnlyList<Vector3> localPoints, Vector3 position, float yawDegrees = 0f)
    {
        System.Numerics.Vector3[] points = new System.Numerics.Vector3[localPoints.Count];
        for (int i = 0; i < localPoints.Count; i++)
        {
            points[i] = localPoints[i].ToNum();
        }

        ConvexHull hull = new(points, _simulation.BufferPool, out System.Numerics.Vector3 centre);

        // BEPU recentres the hull on its computed centre, so the pose must be offset by it or the shape ends
        // up shifted away from the requested position. This is the transform the engine collides against.
        Matrix rotation = Matrix.CreateRotationY(MathHelper.ToRadians(yawDegrees));
        Vector3 worldCentre = Vector3.Transform(centre.ToGame(), rotation);
        Matrix transform = rotation * Matrix.CreateTranslation(position + worldCentre);

        TypedIndex index = _simulation.Shapes.Add(hull);
        _simulation.Statics.Add(new StaticDescription(ToRigidPose(transform), index));

        // The engine holds the hull's vertices relative to `centre`, so that is how they must be walked
        // through `transform` to land in the world: R * (p - centre) + (position + R * centre) == R * p + position.
        Vector3 centreGame = centre.ToGame();
        Vector3[] worldPoints = new Vector3[localPoints.Count];
        for (int i = 0; i < localPoints.Count; i++)
        {
            worldPoints[i] = Vector3.Transform(localPoints[i] - centreGame, transform);
        }

        _shapes.Add(new ColliderShape(
            ColliderKind.ConvexHull,
            transform,
            BoundsOf(worldPoints),
            Vector3.Zero,
            0f,
            0f,
            worldPoints,
            false));
    }

    private static Matrix PoseOf(Vector3 position, float yawDegrees) =>
        Matrix.CreateRotationY(MathHelper.ToRadians(yawDegrees)) * Matrix.CreateTranslation(position);

    private static RigidPose ToRigidPose(Matrix transform)
    {
        Vector3 position = transform.Translation;
        Quaternion rotation = Quaternion.CreateFromRotationMatrix(transform);
        return new RigidPose(position.ToNum(), rotation.ToNum());
    }

    private static BoundingBox WorldBoundsOf(Vector3 size) => new(-size * 0.5f, size * 0.5f);

    private static BoundingBox BoundsOf(IReadOnlyList<Vector3> points)
    {
        Vector3 min = points[0];
        Vector3 max = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        return new BoundingBox(min, max);
    }

    // ------------------------------------------------------------------ bodies

    /// <summary>
    /// Adds a dynamic capsule, rotation-locked by default: the shape a player or upright enemy wants.
    /// </summary>
    public PhysicsBody AddDynamicCapsule(float radius, float segmentLength, Vector3 position, float mass, bool lockRotation = true)
    {
        Capsule capsule = new(radius, segmentLength);

        BodyDescription description = new()
        {
            Pose = new RigidPose(position.ToNum()),
            Velocity = default,
            LocalInertia = new BodyInertia
            {
                InverseMass = 1f / mass,
                InverseInertiaTensor = lockRotation ? default : capsule.ComputeInertia(mass).InverseInertiaTensor,
            },
Collidable = new CollidableDescription(_simulation.Shapes.Add(capsule)),
        };

        BepuPhysics.BodyHandle handle = _simulation.Bodies.Add(description);
        PhysicsBody body = new(this, handle, mass);
        _bodies[handle] = body;
        _dynamicCollidables.Add(body.Collidable.Packed);

        _shapes.Add(new ColliderShape(
            ColliderKind.Capsule,
            Matrix.CreateTranslation(position),
            WorldBoundsOf(new Vector3(radius * 2f, segmentLength + (radius * 2f), radius * 2f)),
            Vector3.Zero,
            radius,
            segmentLength,
            Array.Empty<Vector3>(),
            true));

        return body;
    }

    /// <summary>
    /// Replaces a dynamic body's capsule with one of different proportions, keeping the body in place.
    ///
    /// BEPU stores a shape index on the body's collider, so a slide only has to point that index at a
    /// different capsule. The body's feet must stay put: shrinking a capsule whose centre does not move
    /// would lift the player off the floor, so the caller supplies the new centre and this keeps the
    /// existing velocity untouched.
    /// </summary>
    public void ReshapeCapsule(BepuPhysics.BodyHandle handle, float radius, float segmentLength)
    {
        Capsule replacement = new(radius, segmentLength);
        TypedIndex index = _simulation.Shapes.Add(replacement);

        BodyReference reference = _simulation.Bodies.GetBodyReference(handle);
        _simulation.Awakener.AwakenBody(handle);
        reference.Collidable.Shape = index;
        _simulation.Bodies.UpdateBounds(handle);
    }

    /// <summary>
    /// Recomputes a body's broad-phase bounds from its current pose and shape. Must be called after a reshape
    /// and after any direct pose change, or the broad phase keeps working from the pose it last saw.
    /// </summary>
    public void RefreshBounds(BepuPhysics.BodyHandle handle) => _simulation.Bodies.UpdateBounds(handle);

    /// <summary>
    /// True when a capsule of the given size would overlap static geometry at <paramref name="position"/>.
    ///
    /// Used to decide whether a sliding player has room to stand back up. BEPU's narrow phase is built for
    /// simulation rather than ad-hoc queries, so this sweeps the capsule's own volume with a small bundle of
    /// rays: one up the axis plus a ring around it. That is conservative - it reports a blocked capsule
    /// slightly early - which is the right way to be wrong for standing up under a low ceiling.
    /// </summary>
    public bool CapsuleBlocked(Vector3 position, float radius, float totalHeight, CollidableId ignore)
    {
        float halfSegment = MathF.Max(0f, (totalHeight - (radius * 2f)) * 0.5f);
        float bottom = position.Y - halfSegment - radius;
        float top = position.Y + halfSegment + radius;

        // Straight up the axis from just above the feet.
        if (OverlapsAlongRay(new Vector3(position.X, bottom + 0.01f, position.Z), top - bottom, ignore))
        {
            return true;
        }

        // A ring at the widest part of the capsule, where a ceiling edge is most likely to clip it.
        const int RingSamples = 8;
        for (int i = 0; i < RingSamples; i++)
        {
            float angle = MathHelper.TwoPi * i / RingSamples;
            float x = position.X + (MathF.Cos(angle) * radius);
            float z = position.Z + (MathF.Sin(angle) * radius);

            if (OverlapsAlongRay(new Vector3(x, bottom + 0.01f, z), top - bottom, ignore))
            {
                return true;
            }
        }

        return false;
    }

    private bool OverlapsAlongRay(Vector3 origin, float length, CollidableId ignore)
    {
        RayHit hit = Raycast(origin, Vector3.Up, length, ignore);
        return hit.Hit && hit.Distance <= length;
    }

    internal void Forget(BepuPhysics.BodyHandle handle)
    {
        if (_bodies.Remove(handle, out PhysicsBody? body))
        {
            // The collidable's id has to be retired with the body, or a later ray query would treat a recycled id
            // as still dynamic and silently skip world geometry at that spot.
            _dynamicCollidables.Remove(body.Collidable.Packed);
        }

        _simulation.Bodies.Remove(handle);
    }

    internal BodyReference ReferenceOf(BepuPhysics.BodyHandle handle) => _simulation.Bodies.GetBodyReference(handle);

    internal void Wake(BepuPhysics.BodyHandle handle) => _simulation.Awakener.AwakenBody(handle);

    // ---------------------------------------------------------------- stepping

    /// <summary>
    /// How many fixed steps are needed to advance by <paramref name="frameDeltaSeconds"/>. Callers run
    /// their gameplay update once per returned step, which is what makes movement frame-rate independent.
    /// </summary>
    public int CalculateSubStepCount(float frameDeltaSeconds)
    {
        if (frameDeltaSeconds > 0f)
        {
            _accumulator += frameDeltaSeconds;
        }

        // Counting in one go and subtracting in bulk, plus a small tolerance, keeps a frame rate that is an
        // exact multiple of the fixed step from occasionally losing or gaining a step to rounding.
        int steps = (int)Math.Floor((_accumulator + StepTolerance) / PhysicsDefaults.FixedTimeStep);
        if (steps <= 0)
        {
            return 0;
        }

        _accumulator -= steps * PhysicsDefaults.FixedTimeStep;

        if (steps >= PhysicsDefaults.MaximumSubStepsPerFrame)
        {
            // Out of budget: drop the backlog rather than accumulate debt we could never repay.
            steps = PhysicsDefaults.MaximumSubStepsPerFrame;
            _accumulator = 0f;
        }

        return steps;
    }

    /// <summary>
    /// Registers a callback to run before each fixed step. Fire-and-forget; there is no unregister, so a caller
    /// that can be torn down should be torn down with the world.
    /// </summary>
    public void AddPreStepCallback(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _preStepCallbacks.Add(callback);
    }

    /// <summary>
    /// Advances the simulation by exactly one fixed step. Pre-step callbacks run first, so velocity written by
    /// one is integrated by this step.
    /// </summary>
    public void StepSubStep()
    {
        foreach (Action callback in _preStepCallbacks)
        {
            callback();
        }

        _simulation.Timestep(PhysicsDefaults.FixedTimeStep);
    }

    // ----------------------------------------------------------------- queries

    /// <summary>Casts a ray and reports the closest hit, optionally ignoring one collidable.</summary>
    public RayHit Raycast(Vector3 origin, Vector3 direction, float maxDistance, CollidableId ignore = default)
    {
        Collector collector = new(ignore, ignoreDynamicBodies: false, _dynamicCollidables);
        System.Numerics.Vector3 start = origin.ToNum();
        System.Numerics.Vector3 delta = direction.ToNum();
        _simulation.RayCast(in start, in delta, maxDistance, ref collector);

        Vector3 point = origin + (direction * collector.Distance);
        return new RayHit(collector.Hit, point, collector.Normal.ToGame(), collector.Distance, new CollidableId(collector.Collidable.Packed));
    }

    /// <summary>
    /// Casts a ray against world geometry only, ignoring every dynamic body.
    ///
    /// Projectiles need this: a rocket fired from the player's own hand must not detonate on the player's
    /// capsule, and a projectile sweeping towards a target should not be stopped by a body that happens to be
    /// between them. Dynamic-vs-static is a question only the engine can answer, which is why this lives here
    /// rather than being filtered by the caller.
    /// </summary>
    public RayHit RaycastStatic(Vector3 origin, Vector3 direction, float maxDistance, CollidableId ignore = default)
    {
        Collector collector = new(ignore, ignoreDynamicBodies: true, _dynamicCollidables);
        System.Numerics.Vector3 start = origin.ToNum();
        System.Numerics.Vector3 delta = direction.ToNum();
        _simulation.RayCast(in start, in delta, maxDistance, ref collector);

        Vector3 point = origin + (direction * collector.Distance);
        return new RayHit(collector.Hit, point, collector.Normal.ToGame(), collector.Distance, new CollidableId(collector.Collidable.Packed));
    }

    /// <summary>
    /// True when a collidable belongs to a dynamic body rather than to world geometry.
    ///
    /// Tracked as a set rather than queried from the engine: a <c>CollidableReference</c> packs a handle that is
    /// only meaningful alongside the tree it came from, so there is no correct way to test it in isolation. The
    /// set is populated where bodies are created and cleared where they are removed, which keeps it exact for
    /// the lifetime of the world.
    /// </summary>
    public bool IsDynamic(CollidableId collidable) =>
        collidable.Exists && _dynamicCollidables.Contains(collidable.Packed);

    /// <summary>True when a surface at least as steep as <paramref name="minNormalY"/> is hit below.</summary>
    public bool ProbeGround(Vector3 origin, float maxDistance, float minNormalY, CollidableId ignore, out Vector3 normal)
    {
        RayHit hit = Raycast(origin, Vector3.Down, maxDistance, ignore);
        normal = hit.Normal;
        return hit.Hit && hit.Normal.Y >= minNormalY;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _simulation.Dispose();
    }

    /// <summary>Closest-hit collector; a struct so ray queries allocate nothing.</summary>
    private struct Collector : IRayHitHandler
    {
        private readonly CollidableId _ignore;
        private readonly bool _ignoreDynamic;
        private readonly HashSet<uint> _dynamic;

        public Collector(CollidableId ignore, bool ignoreDynamicBodies, HashSet<uint> dynamicCollidables)
        {
            _ignore = ignore;
            _ignoreDynamic = ignoreDynamicBodies;
            _dynamic = dynamicCollidables;
            Hit = false;
            Distance = float.MaxValue;
            Normal = System.Numerics.Vector3.Zero;
            Collidable = default;
        }

        public bool Hit { get; private set; }

        public float Distance { get; private set; }

        public System.Numerics.Vector3 Normal { get; private set; }

        public CollidableReference Collidable { get; private set; }

        // A dynamic body is skipped entirely rather than counted and rejected later, so the ray passes through
        // it and can still find the wall behind.
        //
        // Tested against the world's own registry rather than by inspecting the packed collidable. BEPU packs a
        // tree index into that value, and bit 31 means "static" rather than "dynamic" - reading it as a dynamic
        // flag silently rejected every wall in the arena, which is exactly what projectiles must hit.
        public bool AllowTest(CollidableReference collidable) =>
            _ignore.Packed != collidable.Packed
            && !(_ignoreDynamic && _dynamic.Contains(collidable.Packed));

        public bool AllowTest(CollidableReference collidable, int childIndex) => AllowTest(collidable);

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in System.Numerics.Vector3 normal, CollidableReference collidable, int childIndex)
        {
            if (Hit && t >= Distance)
            {
                return;
            }

            Hit = true;
            Distance = t;
            Normal = normal;
            Collidable = collidable;
        }

    }
}
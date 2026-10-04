# Architecture

Rules of the house:

1. **One responsibility per file, one concept per class.** If a file needs a "and" in its summary, split it.
2. **No engine types leak into gameplay code** beyond `Vector3`/`Matrix`/`Color`. Gameplay must be
   testable without a `GraphicsDevice`.
3. **No system is created before something needs it.** Empty classes and `TODO` shells are not allowed.
4. **Every tunable number is a named constant or config field.** Never a magic number buried in logic.
5. **Verify, do not assume.** After changing rendering, run `--selftest` and look at the PNG.

## Module map

```
Program.cs                       process entry, argument errors -> exit codes
 └── App/GameApp.cs              composition root + main loop. Owns every other system.
      ├── App/AppConfig.cs       window, camera and self-test settings, parsed from the CLI
      ├── Camera/                CameraRig -> FlyCamera / OrbitCamera, CameraInput, CameraController
      ├── Map/                   World (draws), Renderable, TestArena (map authoring)
      ├── Graphics/              MeshBuilder -> MeshData -> GpuMesh, SurfaceMaterial
      ├── Diagnostics/           SelfTestSession
      └── Core/                  Log, MathHelpers, PngWriter, ConsoleHost
```

`GameApp` is the only place allowed to know about all of the others. Systems never construct each other.

## Layers and their contracts

### Core (`MovementShooter.Core`)
Engine-agnostic helpers. `Log` writes to console + `logs/movement-shooter.log`. `MathHelpers` holds the
clamp/lerp/smoothstep/deadzone helpers that movement code will reuse. `PngWriter` is a BCL-only PNG
encoder used by the self-test so screenshots need no image library.

### Graphics (`MovementShooter.Graphics`)
* `MeshBuilder` — procedural geometry (box, ramp wedge, cylinder). Emits counter-clockwise winding as seen
  from outside, so face normals can be verified by hand with a cross product.
* `MeshData` — immutable CPU mesh: vertices, indices, triangle count.
* `GpuMesh` — vertex/index buffers for one `MeshData`, 16-bit indices with a 32-bit fallback.
* `SurfaceMaterial` — wrapper over the built-in `BasicEffect`: two factories, `CreateLit` (directional
  light + ambient + specular) and `CreateUnlit`. Per-object colour comes from vertex colours, so a
  handful of materials serve the whole map. Materials are created once and shared.

**Winding gotcha (already paid for once):** the builders emit mathematically counter-clockwise winding,
while MonoGame's default `RasterizerState.CullCounterClockwise` treats clockwise as front-facing in its
left-handed space. `World.FrontFaceState` sets `CullClockwise` so the intended faces survive culling. If
you add geometry, keep the same winding rule; if you change the state, change it in exactly one place.

Vertex format is `VertexPositionColorNormal` (position, colour, normal) with **no UVs**, because there is
no texture pipeline yet — the whole map is vertex-coloured. Adding textures means picking a format with
`TexCoord0` and revisiting `MeshBuilder`.

### Camera (`MovementShooter.Camera`)
`CameraRig` exposes `Position`, `Target`, view/projection matrices and the verbs `Look`, `Move`, `Zoom`,
`Reset`. `FlyCamera` is the spectator camera used by the bootstrap; the player camera will be a new rig
(or a child of `CameraRig`) so rendering code does not change when the player controller arrives.
`CameraInput` only polls devices into a frame snapshot, which is what makes input replaceable by a
replay or an automated test.

### Map (`MovementShooter.Map`)
`World` owns a flat list of `Renderable` (mesh + transform + material) and draws them. `TestArena` authors
the test playground. `World.CreateMesh` uploads a `MeshData` once so `World.Add(mesh, ...)` can place the
same geometry several times without duplicating GPU buffers. There is deliberately **no** spatial
partitioning, frustum culling or batching yet: at 27 draw calls for the test arena it would be noise.
Add them when the map grows enough to need them, together with the bounds computation they depend on.

### Diagnostics (`MovementShooter.Diagnostics`)
`SelfTestSession` renders into an offscreen `RenderTarget2D`, screenshots it and reports coverage, colour
diversity and average brightness. `GameApp` turns that into an exit code. Anything visually verifiable
should get a check here so regressions are caught without a human at the keyboard.

## Planned systems (not built yet)

Build in this order; each step must keep `--selftest` green.

| Milestone | Modules | Notes |
| --- | --- | --- |
| 2. Player + physics | `Player/Player.cs`, `Player/PlayerMovement.cs`, `Physics/*` | Introduce BEPUv2 for the character capsule. Ground/air state machine, momentum preservation |
| 3. Weapons | `Weapons/*`, `Weapons/RocketLauncher.cs`, `Weapons/Melee/*` | Hitscan/projectile separation; the pan/tool mechanic |
| 4. Rockets | `Projectiles/Rocket.cs`, `Combat/Explosion.cs` | Owns knockback impulses applied to *any* body, including the player |
| 5. Enemies | `Entities/Enemy.cs`, ... | Reuse the damage/knockback pipeline the rocket needs |
| 6. Effects | `Effects/*` | Particles, tracers, screen shake, impact decals |
| 7. HUD | `UI/Hud.cs`, ... | Needs a font; comes with the content pipeline decision |
| 8. Map v2 | `Map/MovementTestMap.cs` | Rocket-jump sized gaps, risk/reward routes |

The rocket is a *physics force*, not a damage number: `Explosion` should apply an impulse to every body in
radius, which is what makes rocket jumping fall out of the same code path as enemy knockback.

## Conventions

* `sealed` by default; `IDisposable` types must be idempotent and are released in `GameApp.UnloadContent`.
* Timestep: delta seconds are clamped to `MaxDeltaSeconds` (0.1) so a hitch cannot tunnel anything.
  Movement work will likely need fixed sub-steps — decide that with the physics integration, not before.
* Frames are counted in `Draw`, not `Update`: with a fixed timestep MonoGame may run several `Update`
  calls per `Draw`.
* Namespaces match folders; the map namespace is `Map` (not `World`) so the `World` class name is usable.
* Keep public surface small. If a member is only used by one caller, make it internal or move it.
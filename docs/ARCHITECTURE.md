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
Program.cs                       process entry, headless checks, argument errors -> exit codes
└── App/GameApp.cs                composition root + main loop. Owns every other system.
     ├── App/AppConfig.cs         window, camera and self-test settings, parsed from the CLI
     ├── App/GameName.cs          the KINETIC branding constant
     ├── Map/                     MapBlock -> TestArena (data), MapRenderer, MapColliders, World, Renderable
     ├── Physics/                 PhysicsWorld, PhysicsBody, PhysicsDefaults
      ├── Player/                  PlayerController, Player, PlayerMovement, PlayerCamera,
      │                            PlayerInput, PlayerTuning, CursorLock
      ├── Combat/                  Health, DamageInfo/IDamageable, ExplosionFalloff, CombatTarget,
      │                            ExplosionSystem. No knowledge of weapons - damage is not weapon-specific.
      ├── Weapons/                 WeaponController, Weapon, RocketLauncher, WeaponTuning,
      │                            ProjectileSystem, WeaponInput
      ├── Targets/                 TestDummy, TestDummyRenderer
      ├── UI/                      BitmapFont, TextRenderer, PlayerDebugOverlay,
      │                            MovementEffectRenderer, CombatEffectRenderer
      ├── Graphics/                MeshBuilder -> MeshData -> GpuMesh, SurfaceMaterial
      ├── Diagnostics/             SelfTestSession (render), PhysicsSelfTest (player, physics and combat)
     └── Core/                    Log, MathHelpers, PngWriter, ConsoleHost
```

`GameApp` is the only place allowed to know about all of the others. Systems never construct each other.

## Naming

The product is **KINETIC** (`App/GameName.cs` is the single source of truth for the window title, CLI text
and log file name). The assembly, namespaces, folders, solution, executable and GitHub repository keep
the name `MovementShooter`: those are structural, not cosmetic, and renaming them would touch every
import and build path for no gameplay benefit. Build metadata (`Product`, `AssemblyTitle`, `Description`)
carries the KINETIC name.

## Layers and their contracts

### Core (`MovementShooter.Core`)
Engine-agnostic helpers. `Log` writes to console + `logs/kinetic.log`. `MathHelpers` holds the
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

### Map (`MovementShooter.Map`)
Level geometry is data: a `MapBlock` is a shape, a position, a yaw and a colour. `MapRenderer` turns blocks
into GPU meshes and `MapColliders` turns the same blocks into static colliders, so what the player sees and
what the player collides with cannot drift apart. `World` owns the resulting renderables and draws them.

`World.CreateMesh` uploads a `MeshData` once so `World.Add(mesh, ...)` can place the same geometry several
times without duplicating GPU buffers. There is deliberately **no** spatial partitioning, frustum culling
or batching yet: the arena is 27 draw calls, so anything more would be noise. Add them when the map grows.

### Physics (`MovementShooter.Physics`)
`PhysicsWorld` is the only class that knows BEPUv2 exists. Gameplay asks for shapes, bodies, fixed steps
and ray queries using MonoGame types; the `System.Numerics` conversions live in `Num`. `PhysicsBody` wraps
one dynamic body and is where the two engine quirks are handled:

* **Waking.** A sleeping body ignores velocity writes, and the engine puts resting bodies to sleep, so
  every write wakes the body first. Without this the player stops responding after standing still.
* **Convex hulls.** BEPU recentres a hull on its computed centre, so the collider pose is offset by that
  centre or the shape ends up somewhere else entirely.

Contacts are frictionless on purpose (`DefaultNarrowPhaseCallbacks`): friction and stick-to-ground are
integrated by `PlayerMovement` instead, which keeps movement predictable.

Steps run at a fixed `PhysicsDefaults.FixedTimeStep` (1/60 s) through an accumulator, capped at
`MaximumSubStepsPerFrame` so a long stall cannot spiral. Callers get `CalculateSubStepCount` and run their
gameplay update once per step — that is the whole mechanism behind frame-rate independence.

### Player (`MovementShooter.Player`)
* `PlayerTuning` — every player number: capsule size and mass, speed, ground/air acceleration, friction,
  gravity, jump speed, coyote time, jump buffer, ground probe, eye height, sensitivity, spawn.
* `Player` — the entity: physics body, `PlayerMovement`, `PlayerCamera`. Two verbs: apply a look delta,
  and `Advance(frameDelta, wishDirection, jumpHeld)` which runs the fixed substeps.
* `PlayerController` — binds devices to the player. Converts WASD into a world direction using the camera
  yaw, applies mouse look through `CursorLock`, and advances the player. It also has a headless mode so
  the automated checks drive the *real* player with no window.
* `PlayerMovement` — the character controller: acceleration and friction, gravity, jump with edge
  detection, and ground detection by ray probes from the capsule centre plus four offsets.
* `CursorLock` — MonoGame has no relative-mouse mode, so capture is "hide the cursor and move it back to
  the centre each frame"; the offset from centre is the look delta. Escape releases, a click recaptures.
* `PlayerInput` — keyboard only, so movement can later be driven by a gamepad, bot or replay.
* `PlayerCamera` — owns yaw and pitch, derives the eye position from the player's current stance so a slide
  lowers the view with the body, and owns the movement feel impulses: a short field-of-view kick on a dash
  and a smaller, sustained widening while sliding. These are additive on `FieldOfViewDegrees` and decay
  exponentially (`UpdateFeel`), so they cannot steer the camera — a player cannot mistake them for aim drift —
  and they fade over the same real time at any frame rate.
* `MovementEffects` / `MovementEffectRenderer` (`MovementShooter.UI`) — the procedural feedback: short dash
  streaks and a few motes of slide dust. Pure geometry and lifetime, no texture and no content pipeline.
  `MovementEffects` holds **no reference to the physics world** and only ever reads movement state; the
  renderer writes one dynamic vertex buffer per frame. That split is what lets the whole thing be tested
  headlessly and is what keeps it structurally incapable of affecting movement — see the check
  "movement effects do not touch the simulation", which runs an identical scenario with and without effects
  and requires bit-identical results. Cues are triggered from `PlayerMovement`'s *running totals* in
  `GameApp.UpdateMovementFeedback`, never from the per-substep flags: a frame running zero substeps never
  sets those flags, so a cue would be missed exactly when the frame rate is highest.

Five things worth remembering when touching movement:

* **Every key press is latched until a fixed substep runs** (`PlayerController.Advance`). At 144 fps and
  above, three frames out of four run *zero* substeps, so a key tapped on exactly one of those frames would be
  sampled and discarded without the movement code ever seeing it — taps vanished at high frame rates. The
  latch is only cleared once a substep has actually consumed it. Do not clear it unconditionally; that
  reintroduces the bug on precisely the frames the latch exists to protect. **All three edge-triggered keys
  must be latched, including the slide** — `PlayerMovement` detects its own rising edge, so the slide is just
  as vulnerable, and a one-frame slide tap did nothing at all above 60 fps until it was found this way.

* **Gravity is always applied, never pinned to zero.** Pinning while grounded was tried and made the
  capsule creep upwards over time; letting the contact solver stop the body is both simpler and stable.
* **Ground friction is applied after the physics step** (`PlayerMovement.AfterStep`), because the solver's
  push on a slope lands after our velocity write. Applying it before the step let the player creep
  downhill at about 9 cm/s.
* **`GroundFriction` only ever applies when there is no movement input.** It is the value that decides how a
  released key feels, and nothing else: it is never a brake on acceleration, so momentum through jumps, air
  movement, slide-jumps and dashes is untouched by it. At 12 m/s² - under a quarter of `GroundAcceleration` -
  releasing a key decelerated far more slowly than pressing one accelerated, and the player coasted for over
  half a second, which felt like being pushed. 45 m/s² sheds a full run in about 0.16 s against 0.08 s to
  reach it: a firm ramp down, not a cut to zero. Lower it and the "ice cube" feel comes straight back.
* **The stance eases toward a target in both directions.** `TryStartSlide` only *aims* at `SlideHeight` and
  `UpdateStance` walks the collider there at `SlideCrouchDownSpeed`; `EndSlide` aims back at full height and
  the same method walks up at `SlideStandUpSpeed`. The entry used to be a single reshape on the slide's first
  frame while the exit was already gradual, so the two ends of one transition felt like different actions — the
  view dropped instantly on the way in. Two things to keep intact: the crouch check has to run **before** any
  "already standing" shortcut, because on the slide's first frame the capsule is still at full height while the
  target has already dropped; and the rising branch must keep its `CapsuleBlocked` headroom test, or a slide
  ending under a low ceiling will force the player upright into it.
* **A dash is a period, not an impulse.** `TryDash` writes the initial velocity and `ApplyDashMovement` then
  owns the horizontal component for `DashDuration`, holding `DashSpeed` along the committed direction and
  easing rather than snapping. Handing the player back to ordinary acceleration immediately is what made a
  dash from a run indistinguishable from running faster: the burst was over in about four frames, ~60 ms.
  Steering is deliberately ignored for the duration so the action stays committed, and gravity is never
  touched, so an air dash keeps its fall rate and a jump is not cancelled. `DashSpeed` is a hard ceiling, so
  repeated dashes compound no further than one dash does.
* **A field-of-view kick is one kick, however often it is asked for.** PlayerCamera kicks are
  *saturating* (Max, not +=), and FieldOfViewDegrees is clamped to a range the projection accepts.
  Both matter because the mouse button can be **held**: a kick that added to itself gained over 400 degrees per
  second, pushed the field of view past 180, and made CreatePerspectiveFieldOfView throw inside Draw - which
  is a fatal, unrecoverable exit, not a dropped frame. Anything the fire path touches must be safe to
  call every frame, even though the key that triggers it is edge-triggered.

And the invariant that caused a wall-climbing bug:

* **"Near the ground" and "standing on something" are different questions.** `AfterStep` answers both:
  `IsGrounded` uses the loose `GroundProbeDistance` (0.15 m) and drives friction, while *jump support* —
  and therefore coyote time — requires a walkable surface within the much tighter `JumpSupportTolerance`
  (0.05 m) **and** not moving upwards. The jump itself is gated on coyote time alone.
  Merging the two (jumping whenever `IsGrounded`, refreshing coyote from the loose probe) let the first
  few centimetres of every jump re-arm coyote, so a second press jumped again in mid-air and the player
  could stack jumps into a hover, most visibly while pressed against a wall. A wall never grants support:
  the walkable-surface rule requires `normal.Y >= MinimumGroundNormalY`, and a vertical wall's normal is
  horizontal. Do not relax `JumpSupportTolerance` or the ascending check without re-running
  `--physics-test`.

### Combat (`MovementShooter.Combat`)
* `Health` - hit points and the whole of the damage model: cumulative, never regenerating, clamped at zero, and
  dead stays dead. Implements `IDamageable`. No armour, resistance, invulnerability window or healing, because
  none of those are designed yet and guessing at them now would only make them harder to change later.
* `DamageInfo` / `IDamageable` - one `TakeDamage` method, and a value carrying amount, source, world position and
  whether it was self-damage. Weapons depend only on `IDamageable`, so anything implementing it can be shot.
* `ExplosionFalloff` - pure functions for the one falloff curve, shared by damage *and* force. Smoothstep over
  `1 - distance/radius`: exactly zero at the edge (so the affected set is a clean boundary rather than a value
  that merely gets small), and symmetric about the midpoint (so half the radius is half strength). Being pure is
  what lets the curve be tested on its own rather than only through a live explosion.
* `CombatTarget` - a body to knock around plus an optional `IDamageable`. Self-damage is recognised by comparing
  a target's `Owner` against the explosion's source; there is no enemy list, because there are no teams yet.
* `ExplosionSystem` - the one implementation of "what an explosion does": find targets in radius, apply damage,
  apply the impulse. **Self-damage goes through exactly this path**, differing only by two scale factors on the
  explosion itself. That is what keeps the two from drifting apart, and it is why a rocket jump needed no
  player-specific code at all.

Impulses are applied with `PhysicsBody.ApplyImpulse`, never by writing velocity. The solver integrates it into
whatever the body already had, so a running player is still running when the blast lands. Writing velocity
would silently discard momentum, and could stop a jump mid-air.

### Weapons (`MovementShooter.Weapons`)
* `WeaponTuning` / `RocketLauncherTuning` - all weapon numbers as data, deliberately *not* in `PlayerTuning`:
  a rocket's speed has no business next to the player's run speed, and changing one must not be able to move the
  other.
* `Weapon` - base class owning the cooldown and nothing else. `TryFire` returns a `Shot` (origin, direction,
  speed, radius, lifetime), or null when cooling down. It spawns nothing itself, which is what lets the same
  projectile code serve a grenade or a mine later.
* `RocketLauncher` - builds shots and, crucially, knows how to turn its tuning into an `Explosion`. Holds no
  projectile state.
* `ProjectileSystem` - owns live projectiles in a pooled list and steps them. **Collision is a swept raycast**:
  each step tests the whole path from the previous position to the new one, against world geometry only. A
  point test at the new position would tunnel - at 42 m/s and a 1/60 s step that is 0.7 m per step, and the arena
  has 0.6 m thick platforms. Sweeping cannot miss whatever the speed. Stepping is registered through
  `PhysicsWorld.AddPreStepCallback`, so projectiles advance in the same fixed substeps as the player and their
  travel cannot become frame-rate dependent.
* `WeaponInput` - left mouse button, and **only while the cursor is captured**. Without that guard, the click that
  *captures* the cursor would also fire the weapon.
* `WeaponController` - the only class gameplay asks to fire. Owns the equipped weapon and the projectile system.

### Targets (`MovementShooter.Targets`)
* `TestDummy` - a dynamic capsule with health and a bright box body. Dynamic rather than static on purpose: a
  static body could not be knocked back, and knockback is half of what it exists to demonstrate. No AI, no
  behaviour - only health and a body to throw.
* `TestDummyRenderer`, `UI.CombatEffectRenderer` - procedural and untextured. Two shared meshes and one unlit
  effect; health bars and rocket bodies are billboards and cylinder instances rather than per-object meshes.

### First-person weapon (`Weapons.WeaponViewModel`, `UI.FirstPersonWeaponRenderer`)
* `WeaponViewModel` - presentation only: where the model sits relative to the eye, and its recoil and muzzle-flash
  state. No graphics types and no reference to the camera, which is what lets the recoil timing and the flash be
  checked headlessly instead of by eye. The recoil is a damped cosine of the time since the shot, not a
  per-frame spring, so it is frame-rate independent for free - it has to be, because a *held* mouse button would
  otherwise integrate it at the frame rate.
### Diagnostics (`MovementShooter.Diagnostics`)
Two suites, both driven from `Program` and both exiting with a status code.

* `PhysicsSelfTest` runs headless (no window, no GPU) and drives the real `PlayerController` through a real
  physics world: falling, resting, not falling through the floor, standing still, reaching the configured
  speed, jump height, held-jump handling, air-jump prevention, ramp climbing, mouse-look clamping and
  frame-rate independence. Anything that can be asserted without a GPU belongs here.
* `SelfTestSession` renders into an offscreen `RenderTarget2D`, screenshots it and reports coverage, colour
  diversity and average brightness. Anything visually verifiable belongs here.

Keep both green before calling any milestone done.

## Planned systems (not built yet)

Build in this order; each step must keep `--physics-test` and `--selftest` green.

Milestones 1-5 are built. 3 and 4 landed inside `PlayerMovement` rather than as separate files, because slide and
dash are movement states rather than independent systems; 5 is the `Combat` + `Weapons` + `Targets` trio above.

| Milestone | Modules | Notes |
| --- | --- | --- |
| 6. Weapons | `Weapons/WeaponSwitcher.cs`, `Weapons/Melee/*` | Weapon switching, and the pan/tool mechanic. `Weapon` and `WeaponController` already separate per-weapon behaviour from what the player owns |
| 7. Enemies | `Entities/Enemy.cs`, ... | Reuse `IDamageable`, `CombatTarget` and the explosion path; the rocket already needs all three |
| 8. Effects | `Effects/*` | Particles, tracers, screen shake, impact decals. Current rockets and blasts are two shared meshes each |
| 9. HUD | `UI/Hud.cs`, ... | Needs a real font; comes with the content pipeline decision |
| 10. Map v2 | `Map/MovementArena.cs` | Rocket-jump sized gaps, risk/reward routes |

The rocket was built as a *physics force*, not a damage number: `ExplosionSystem` applies an impulse to every
body in radius through `PhysicsBody.ApplyImpulse`, which is what makes rocket jumping fall out of the same code
path as anything else that gets hit. Damage and knockback deliberately share one falloff curve, so a target can
never be badly hurt but barely pushed.

Known limitations of the combat foundation, all deliberate for this milestone: there is no line-of-sight check,
so a blast damages through walls; death is a log line and nothing else, with no respawn; and there is no ammo,
so firing is limited only by the fire interval and a live-rocket ceiling.

## Conventions

* `sealed` by default; `IDisposable` types must be idempotent and are released in `GameApp.UnloadContent`.
* Timestep: frame delta is clamped to `MaxDeltaSeconds` (0.1) so a hitch cannot tunnel anything, then
  gameplay runs in fixed `PhysicsDefaults.FixedTimeStep` substeps.
* Frames are counted in `Draw`, not `Update`: with a fixed timestep MonoGame may run several `Update`
  calls per `Draw`.
* Namespaces match folders; the map namespace is `Map` (not `World`) so the `World` class name is usable.
* No gameplay code may reference BEPUv2 or `System.Numerics`; conversions belong in `Physics/Num`.
* Keep public surface small. If a member is only used by one caller, make it internal or move it.
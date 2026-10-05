# KINETIC

A fast-paced, skill-based 3D movement shooter built on **C# + MonoGame** (DesktopGL) with **BEPUv2**
physics. No Unity, Unreal or Godot. The design goal is *movement + physics + weapon technique +
outplaying* — not generic FPS aim.

**Status: milestone 5 complete.** The movement system is finished and manually tuned (ground braking, air
control, slide, slide-jump, dash, air dash), and the combat foundation is in place behind the first real weapon:
a rocket launcher that flies a physical projectile, collides by sweep, and applies damage and knockback through
one shared explosion path. **Rocket jumping works, and it is not special-cased** — the player is a combat target
like anything else, so the launch is the ordinary impulse reaching an ordinary body.

No enemies, weapon switching, ammo or HUD yet, by design.

> **Naming:** the game is **KINETIC**. The assembly, namespaces, folders, solution and executable keep the
> name `MovementShooter` (and so does the GitHub repository) — renaming those would churn every path and
> import for no gameplay benefit. Everything a player or user actually *sees* — window title, CLI, log
> file, build metadata, docs — is KINETIC, driven by the single `App/GameName.cs` constant.

## Quick start

```powershell
dotnet build MovementShooter.sln -c Debug
dotnet run --project src/MovementShooter/MovementShooter.csproj
```

Or run the built executable directly:

```powershell
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe
```

## Controls

| Input | Action |
| --- | --- |
| Mouse | Look (the cursor is captured automatically when the game starts) |
| `W` `A` `S` `D` | Move |
| `Space` | Jump — holding it does not re-trigger; coyote time and a jump buffer make it forgiving |
| `Ctrl` | Slide — needs speed and ground; sliding then jumping keeps the slide's momentum |
| `Shift` | Dash — edge-triggered, so holding it fires once. Costs the air dash if used in mid-air |
| Left click | **Fire** (once the cursor is captured). Also recaptures the cursor when it is released |
| `Escape` | Release the cursor |
| `F1` | Release or capture the cursor |
| `Alt`+`F4` | Quit |

Rocket jumping: fire at the floor near your feet. The blast throws you up *and* costs you health — the more
aggressive the jump, the more of both. There are four test dummies south of the spawn, in a line, so walking
past them shows the damage and knockback falloff directly.

The overlay in the top-left corner is a development readout (grounded state, position, velocity, horizontal
speed, coyote time, stance, dash, health, weapon, rockets in flight, last explosion, frame rate). It is not the
HUD; the HUD is a later milestone.

## Automated verification

Two suites, both runnable without a human at the keyboard.

### Player and physics checks (no window, no GPU)

```powershell
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe --physics-test
```

Drives real players and real rockets through real physics worlds and checks three groups:

* **Movement** — falling, resting, walk speed, jump height and buffering, coyote time, air control, ramps, walls,
  slide and slide-jump, dash and air dash, stance transitions, frame-rate independence at 30/60/144/240 fps,
  and that a one-frame key tap is never lost at any frame rate.
* **Combat** — damage accumulation and death, the explosion falloff curve, radius limits, knockback direction and
  additivity, rocket travel and swept collision, and frame-rate-independent projectile motion.
* **Rocket jumping** — that a floor blast launches the player through the *ordinary* explosion path, preserves
  horizontal momentum, keeps gravity active, and applies self-damage through the same `Health` as anyone else.

Exits non-zero if anything fails.

```
[PASS] rocket jump launches the player through ordinary explosion knockback - a rocket at the player's feet
       gave a peak of 9.87 m/s up ... and reached 2.30 m of height against a 1.64 m standing jump
[PASS] explosion adds to existing velocity rather than replacing it - velocity went (0.00, 4.00, -6.00) ->
       (0.00, 14.49, -6.00): exactly impulse/mass added on Y
All 73 checks passed.
```

### Render check (writes a screenshot you can look at)

```powershell
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe --selftest
```

Renders offscreen, writes a PNG to `artifacts/`, prints pixel statistics and exits with code `0` when the
scene rendered correctly (`1` when the frame is empty). This is the fastest way to confirm the renderer
still works after a change.

```
Self-test report: pixels=921600 nonClear=527133 coverage=57.2 % colors=38 avg=0.25/0.27/0.32
Self-test PASSED: 3D scene rendered as expected.
```

### Command line

| Flag | Meaning |
| --- | --- |
| `--selftest` | Offscreen render + PNG + pixel report, then exit |
| `--physics-test` | Headless player and physics checks, then exit |
| `--frames N` | Rendered-frame budget (default: unlimited, 6 for `--selftest`) |
| `--out DIR` | Screenshot output directory (default `artifacts`) |
| `--width N` / `--height N` | Window size |
| `--nodebug` | Hide the development player readout |
| `--novsync` | Disable vertical sync |
| `--help` | Usage |

Exit codes: `0` success, `1` checks failed, `2` bad arguments, `3` unhandled error.

Logs go to the console *and* to `logs/kinetic.log` next to the executable.

## The test world

`TestArena` is a small movement playground, not decoration — it exists so that scale, verticality and
feel can be judged before weapons exist:

* 200 × 200 m checkerboard ground (5 m cells) for judging speed and distance
* Four perimeter walls for occlusion testing
* A stepped route (2 m → 4 m → 6 m) and three ramps for the "high route / low route" split
* Floating platforms at 7 m, 11 m and 15 m with gaps between them
* Six pillars of increasing height (5 m … 24 m) as distance markers
* Orange rocket-pad discs marking intended rocket-jump launch spots
* Cyan 1 m / 2 m / 3 m / 4 m cubes and a 2 × 4 × 2 m block for scale reference

The map is described once, as data (`MapBlock`), and consumed twice: `MapRenderer` builds the meshes and
`MapColliders` builds the physics shapes. What you see and what you collide with cannot drift apart.

## Tech decisions (and why)

| Decision | Reason |
| --- | --- |
| `MonoGame.Framework.DesktopGL` 3.8.5.1 | Builds with plain `dotnet build`; no Visual Studio workload or Windows SDK needed |
| Target framework `net8.0` | MonoGame ships a `net8.0` assembly only (verified against the installed SDKs) |
| **No content pipeline (MGCB)** | `MonoGame.Content.Builder.Editor` is not published on nuget.org, so `.mgcb` cannot be built here. Geometry is procedural, lighting uses MonoGame's built-in `BasicEffect`, and even the debug font is generated in code |
| Physics: **BEPUv2 2.4.0** (MIT) | A real rigid-body simulation rather than hand-rolled collision — that is what makes rocket knockback, momentum preservation and physics tricks possible later. Fully wrapped by `Physics/PhysicsWorld`, so no gameplay code touches engine types |
| Character movement integrated by us | Gravity, acceleration, friction and jump are ours; the solver only resolves contacts. Predictable and tunable, which matters more than physical accuracy in a movement shooter |
| Two NuGet dependencies | `MonoGame.Framework.DesktopGL` and `BepuPhysics`, nothing else |

Movement runs in fixed 1/60 s substeps with an accumulator, so behaviour is identical at any frame rate —
verified automatically at 30/60/144/240 fps.

Consequences of "no content pipeline" for later: there is no `.mgcb`, so models and textures are not
loaded. When art is needed, either keep generating meshes procedurally or add MGCB plus a vertex format
with `TexCoord0`. That decision is deferred on purpose.

## Layout

```
MovementShooter.sln                (assembly/namespace names stay; the game is KINETIC)
.editorconfig                      Formatting rules for C#
Directory.Build.props              Shared compiler settings
src/MovementShooter/
  Program.cs                       Entry point, headless checks, exit codes
  App/                             GameApp (bootstrap + main loop), AppConfig, GameName
  Core/                            Log, MathHelpers, PngWriter, ConsoleHost (no engine deps)
  Graphics/                        MeshBuilder, MeshData, GpuMesh, SurfaceMaterial
  Physics/                         PhysicsWorld, PhysicsBody (BEPUv2 wrapper), PhysicsDefaults
  Player/                          Player, PlayerController, PlayerMovement, PlayerCamera,
                                   PlayerInput, PlayerTuning, CursorLock
  Combat/                          Health, IDamageable/DamageInfo, ExplosionFalloff, CombatTarget,
                                   ExplosionSystem (knows nothing about weapons)
  Weapons/                         WeaponController, Weapon, RocketLauncher, WeaponTuning,
                                   ProjectileSystem (swept collision), WeaponInput
  Targets/                         TestDummy, TestDummyRenderer
  Map/                             MapBlock, TestArena (data), MapRenderer, MapColliders, World, Renderable
  UI/                              BitmapFont, TextRenderer, PlayerDebugOverlay (development only),
                                   MovementEffectRenderer, CombatEffectRenderer
  Diagnostics/                     SelfTestSession (render), PhysicsSelfTest (player, physics, combat)
docs/ARCHITECTURE.md               Module map, conventions, planned systems
```

Orientation convention used by the map: **-Z is north, +Z is south, +X is east, -X is west** (Y is up).

See `docs/ARCHITECTURE.md` for the system layout, coding conventions, the reasoning behind the combat and
weapon design, and the roadmap for weapon switching, enemies, effects and HUD.
# KINETIC

A fast-paced, skill-based 3D movement shooter built on **C# + MonoGame** (DesktopGL). No Unity, Unreal or
Godot. The design goal is *movement + physics + weapon technique + outplaying* — not generic FPS aim.

**Status: milestone 1 complete.** Empty folder → clean MonoGame project → 3D rendering → camera → test
world → verified build and run. No gameplay yet, by design.

> **Naming:** the game is **KINETIC**. The assembly, namespaces, folders, solution and executable keep the
> name `MovementShooter` (and so does the GitHub repository) — renaming those would churn every path and
> import for no gameplay benefit. Everything a player or user actually *sees* — window title, CLI, log
> file, build metadata, docs — is KINETIC, driven by the single `App/GameName.cs` constant.

---

## Quick start

```powershell
dotnet build MovementShooter.sln -c Debug
dotnet run --project src/MovementShooter/MovementShooter.csproj
```

Or run the built executable directly:

```powershell
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe
```

### Automated verification (no interaction needed)

```powershell
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe --selftest
```

Renders offscreen, writes a PNG to `artifacts/`, prints pixel statistics and exits with code `0` when the
scene rendered correctly (`1` when the frame is empty). This is the fastest way for a human or an AI to
confirm the renderer still works after a change.

```
Self-test report: pixels=921600 nonClear=604216 coverage=65.6 % colors=29 avg=0.31/0.35/0.37
Self-test PASSED: 3D scene rendered as expected.
```

### Command line

| Flag | Meaning |
| --- | --- |
| `--selftest` | Offscreen render + PNG + pixel report, then exit |
| `--frames N` | Rendered-frame budget (default: unlimited; 6 for `--selftest`) |
| `--out DIR` | Screenshot output directory (default `artifacts`) |
| `--width N` / `--height N` | Window size |
| `--novsync` | Disable vertical sync |
| `--help` | Usage |

Exit codes: `0` success, `1` self-test failed, `2` bad arguments, `3` unhandled error.

Logs go to the console *and* to `logs/kinetic.log` next to the executable.

## Controls (bootstrap spectator camera)

These are temporary scaffolding controls; the real player controller replaces them.

| Input | Action |
| --- | --- |
| Mouse | Look |
| `W` `A` `S` `D` | Move along the ground plane |
| `Q` / `E` | Down / up |
| `Shift` | Boost |
| Mouse wheel | Throttle (fly) / zoom (orbit) |
| `F1` | Switch between fly and orbit camera |
| `R` | Reset the view |
| `Esc` | Quit |

## The test world

`TestArena` is a small movement playground, not decoration. It exists so that scale, verticality and
depth are judged before a player controller exists:

* 200 × 200 m checkerboard ground (5 m cells) for judging speed and distance
* Four perimeter walls for occlusion testing
* A stepped route (2 m → 4 m → 6 m) and three ramps for the "high route / low route" split
* Floating platforms at 7 m, 11 m and 15 m with gaps between them
* Six pillars of increasing height (5 m … 24 m) as distance markers
* Orange rocket-pad discs marking intended rocket-jump launch spots
* Cyan 1 m / 2 m / 3 m / 4 m cubes and a 2 × 4 × 2 m block for scale reference

## Tech decisions (and why)

| Decision | Reason |
| --- | --- |
| `MonoGame.Framework.DesktopGL` 3.8.5.1 | Builds with plain `dotnet build`; no Visual Studio workload or Windows SDK needed |
| Target framework `net8.0` | MonoGame ships a `net8.0` assembly only (verified against the installed SDKs) |
| **No content pipeline (MGCB)** | `MonoGame.Content.Builder.Editor` is not published on nuget.org, so `.mgcb` cannot be built here. Geometry is procedural and lighting uses MonoGame's built-in `BasicEffect` |
| No physics library yet | Nothing needs collision right now. Milestone 2 candidate is **BEPUv2** (pure C#, MIT) — added when the player capsule lands, not before |
| One NuGet dependency | Keeps builds fast and the dependency surface auditable |

Consequences of "no content pipeline" for later: there is no `.mgcb` yet, so models/textures are not
loaded. When art is needed, either keep generating meshes procedurally or add MGCB + a
`VertexPositionColorTexture`-style format. That decision is deferred on purpose.

## Layout

```
MovementShooter.sln                (assembly/namespace names stay; the game is KINETIC)
.editorconfig                   Formatting rules for C#
Directory.Build.props          Shared compiler settings
src/MovementShooter/
  Program.cs                   Entry point, exit codes
  App/       GameApp.cs        Game bootstrap + main loop
              AppConfig.cs     Window, camera and self-test settings, parsed from the CLI
              GameName.cs      The KINETIC branding constant
  Core/                        Log, MathHelpers, PngWriter, ConsoleHost (no engine deps)
  Camera/                      CameraRig + FlyCamera/OrbitCamera + input/controller
  Graphics/                    MeshBuilder, MeshData, GpuMesh, SurfaceMaterial
  Map/                         World, Renderable, TestArena
  Diagnostics/                 SelfTestSession (offscreen render + pixel analysis)
docs/ARCHITECTURE.md           Module map, conventions, planned systems
```

Orientation convention used by the map: **-Z is north, +Z is south, +X is east, -X is west** (Y is up).

See `docs/ARCHITECTURE.md` for the system layout, coding conventions and the roadmap for the player
controller, physics, weapons, enemies, effects and HUD.
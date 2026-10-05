# AGENTS.md — working on KINETIC

Instructions for coding agents on this repository. Read before editing anything.

---

## 1. PROJECT IDENTITY

KINETIC is a fast-paced, skill-based **movement / physics / weapon-technique / outplaying** shooter built on
C# (.NET 8), MonoGame DesktopGL, and BEPUv2 physics.

**Gameplay feel and correctness outrank architectural elegance.** Prefer the smallest change that makes the
gameplay right. A simpler system that feels correct beats a well-layered one that does not.

The assembly, namespaces and solution are named `MovementShooter`. That is deliberate and must not be renamed:
only what a player sees (window title, CLI, logs, docs) is branded KINETIC, via `App/GameName.cs`.

---

## 2. ARCHITECTURE

- Keep systems modular and responsibilities clear: **movement**, **physics**, **weapons**, **combat**,
  **rendering**, **input**, **map**, **diagnostics**.
- **No god objects.** `GameApp` is the only place allowed to know about every other system. Systems never
  construct each other.
- **Do not rewrite or refactor working systems.** Milestones 1–5 are hand-verified. Restructuring for tidiness is
  out of scope; a change must be justified by gameplay, a bug, or a genuine defect.
- Prefer small interfaces at boundaries (`IDamageable`, `Weapon`, `PhysicsBody`), not over-abstracted bases.
- Every gameplay number lives in a tuning class (`PlayerTuning`, `WeaponTuning`, `ViewModelTuning`), never as a
  bare literal in behaviour code. Keep weapon tuning out of `PlayerTuning` and vice versa.
- See `docs/ARCHITECTURE.md` for the module map and the reasoning behind each system.

---

## 3. MOVEMENT INVARIANTS

These are load-bearing. Violating them reintroduces bugs we already found and fixed.

- **Edge-triggered input must be latched until a fixed substep consumes it** (`PlayerController.Advance`).
  At 144 fps and above most frames run *zero* substeps, so a one-frame tap can be sampled and discarded before
  the movement code ever sees it. This applies to **jump, dash and slide** — all three detect their own rising
  edge, so all three are equally vulnerable.
- **Ground friction applies only when there is no movement input.** It must never act as a brake on
  acceleration. Momentum must survive **jumps, air movement, slide-jumps and dashes**.
- **A dash is a short movement *period*, not a one-frame impulse.** It owns the horizontal velocity for
  `DashDuration` and then hands back cleanly, with no snap.
- **Do not loosen ground probes.** A probe that reaches further than the capsule will report a ledge as ground
  and silently grant jumps on walls. Keep `GroundProbeDistance` (loose, drives friction) and
  `JumpSupportTolerance` (tight, arms jump support) as separate answers.
- **Support distance must be measured along the surface normal**, not as raw ray length. A vertical ray on a
  slope overstates the gap and breaks support on every ramp.
- Slide-out was already gradual; slide-in eases to the crouch at `SlideCrouchDownSpeed`. Keep both ends smooth
  and keep the crouch check ahead of any "already standing" shortcut.

---

## 4. CAMERA AND VISUAL INVARIANTS

- **FOV kicks must be saturating, never accumulating.** A kick that adds to itself is invisible for an
  edge-triggered key and catastrophic for a *held* one.
- **Always clamp the projection's field of view to a safe range.** MonoGame throws on FOV ≤ 0 or ≥ 180, and a
  throw inside `Draw` is a fatal, unrecoverable exit — not a dropped frame.
- **Anything reachable from a held input must be safe to call every frame.**
- **Billboard world matrices need a full orthonormal basis** (`Graphics/Billboards`). A zeroed or singular basis
  still positions the quad correctly but cannot be inverted to transform normals, so it shades to black.
- **Billboard normals must face the camera**, or back-face culling makes the quad invisible. Derive facing from
  the camera's *position*, not by inferring it from basis vectors.
- **Keep world rendering and first-person weapon rendering separate.** The viewmodel needs its own pass, its
  own narrower field of view and its own near plane, and a cleared depth buffer so walls never clip it.
- **No unexplained geometry in the gameplay view.** If something renders that should not, find out what it is
  before shipping around it.

---

## 5. PHYSICS RULES

- **Fixed-step simulation only.** Gameplay runs in whole `PhysicsDefaults.FixedTimeStep` substeps. Never step
  gameplay once per frame.
- **Use impulses for knockback** (`PhysicsBody.ApplyImpulse`). An impulse is mass-scaled and *adds* to existing
  momentum, which is what makes rocket jumping compose with running and jumping.
- **Never teleport a dynamic body to fake physics**, and never overwrite velocity where an impulse belongs.
- **Keep visual and collision geometry synchronized.** Map geometry is data-driven (`MapBlock` → `MapRenderer` +
  `MapColliders`) specifically so render/collision drift cannot occur. If you add geometry, add it as data.
- Keep BEPU types inside `Physics/`. Gameplay uses MonoGame types and opaque handles.
- Probe and ray helpers belong in `PhysicsWorld` when the answer requires engine knowledge (e.g. static-vs-dynamic
  filtering) — do not re-derive it per caller.

---

## 6. WEAPON AND COMBAT RULES

- Weapon systems stay modular: `Weapon` owns cooldown and firing, `WeaponController` owns what is equipped,
  `ProjectileSystem` owns flight, `ExplosionSystem` owns blasts. No single all-purpose weapon manager.
- **Firing code must be safe to call every frame while the button is held.** Rate limiting belongs to the
  weapon's cooldown. Feedback must fire only on a *successful* shot.
- **Rocket jumping must emerge from the generic explosion and knockback system.** The player is a
  `CombatTarget` like anything else. **There must never be a `RocketJump()` or any player-only special case.**
- Damage and knockback stay reusable for future enemies and multiplayer: one `IDamageable`, one
  `ExplosionSystem`, one falloff curve shared by damage *and* force.
- **Visual weapon presentation is not gameplay.** Viewmodel, recoil and muzzle flash must not be able to affect
  movement, collision or camera aim.

---

## 7. BLENDER / ART PIPELINE

- **Use Blender MCP** for anything needing substantial 3D work: modeling, rigging, animation, scene inspection.
  Prefer scripted, reproducible generation over hand-tweaking, and inspect the result with viewport screenshots.
- **Do not replace a required visual asset with programmer primitives.** If a proper asset is appropriate, build
  it properly.
- **Visual direction:** stylized, polished **low-poly**. Strong readable silhouettes, a small coherent material
  palette with controlled roughness, slightly exaggerated proportions. **Not photorealism**, and not
  "assembled default primitives".
- Keep geometry deliberately lightweight; do not scatter hundreds of tiny details.
- **Player assets must anticipate multiplayer:** an animation-ready full-body rig, and a *separate*
  first-person presentation asset. Do not merge a weapon into a character mesh, and do not build the whole FPS
  camera system inside Blender.
- **Do not modify gameplay architecture to accommodate one visual asset.** Keep asset size, polycount and rig
  complexity inside what the renderer and the fixed-step budget can actually carry.

---

## 8. DEVELOPMENT WORKFLOW

`PLAN → IMPLEMENT → TEST → MANUAL VERIFY → REVIEW`

For any meaningful change:

1. **Understand the existing architecture first.** Read the code and `docs/ARCHITECTURE.md` before editing.
2. **Plan** — state the approach and the risk before making a large edit.
3. **Implement the smallest coherent change.**
4. **Run the relevant automated checks.**
5. **Manually verify** anything gameplay- or visual-facing.
6. **Review your own diff** before reporting done.

**Do not declare completion on automated tests alone when manual gameplay or visual verification is required.**
A passing suite proves the simulation matches the tests; it does not prove the game is playable. Report what you
actually ran.

---

## 9. VERIFICATION GATES

Before declaring a gameplay, physics or visual change complete:

- [ ] Debug build
- [ ] Release build
- [ ] `--physics-test` (headless player, physics and combat checks)
- [ ] `--selftest` (renders offscreen, writes a screenshot, reports pixel stats)
- [ ] A real windowed run for gameplay or visual changes
- [ ] The **rendered** result inspected for visual changes, not just inferred from code

```powershell
dotnet build MovementShooter.sln -c Debug
dotnet build MovementShooter.sln -c Release
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe --physics-test
src\MovementShooter\bin\x64\Debug\net8.0\win-x64\MovementShooter.exe --selftest
src\MovementShooter\bin\x64\Release\net8.0\win-x64\MovementShooter.exe --frames 300 --novsync
```

There is no `dotnet test` project. The headless suite **is** the test suite.

**If automated tests pass but behaviour is wrong, fix the implementation — do not weaken the test to match.**
Changing an assertion to fit observed behaviour defeats the only regression net this project has.

---

## 10. COMPLETION AND DELEGATION

- **Your final response IS the deliverable.** Never end a turn by saying you are "waiting on" a background task
  — a spawned task is not a completed task, and ending your turn orphans its result.
- **If you delegate, you own collection.** Wait for the result, integrate it, then answer.
- Do not re-delegate work that already fits in one context.
- Report: what changed, which gates were actually run, what remains unverified, known limitations, and files
  touched. Say plainly what you did *not* verify.

---

## 11. GIT

- **Do not commit or push unless explicitly asked.**
- **Never reset, discard or silently overwrite the user's work.** Uncommitted changes here may be deliberate.
- Preserve existing uncommitted changes; inspect `git status` before and after touching anything broad.
- Match the existing commit style when a commit is requested.

---

## 12. STACK-SPECIFIC REMINDERS

`.NET 8`, `Nullable` enabled, `ImplicitUsings` disabled, .NET analyzers on. Project-wide settings live in
`Directory.Build.props`; formatting in `.editorconfig` (4-space indent, braces on new lines).

- No `async`/`await` in gameplay or physics paths — simulation is synchronous and fixed-step.
- Prefer `record` / `record struct` for immutable value-like models; `class` for entities with lifecycle.
- Explicit access modifiers on public and internal APIs.
- 4 spaces, `dotnet format` conventions, no debug statements left behind.
- Tests live in `Diagnostics/PhysicsSelfTest.cs` as named `CheckResult` checks registered in `Run()` — add a
  check there rather than inventing a second test harness.
# McMahon Simulation Methodology

A working framework — distilled from building **Deep Water** — for developing high-fidelity, performant, military 3D simulations in Unity.

This document is **alive**. Principles are added as they're discovered or hardened during real work, and cross-referenced to the code or scene where they were exercised. Vague aspirations get rewritten or removed. The goal is a concrete, falsifiable bible — not a manifesto.

---

## How to use this document

- **When you discover a principle while working, append it.** Don't wait for a dedicated session.
- **Prefer concrete and falsifiable** ("Tuning values live in ScriptableObjects, never as `const` or `[SerializeField]` literals on MonoBehaviours") over vague ("write clean code").
- **Ground every principle in a real case.** Reference the file, scene, system, or commit where the principle was exercised or where its absence caused pain. Abstract principles without provenance get demoted to "Candidates" until a real case anchors them.
- **Diagnose before prescribing.** When a principle came from a legacy code rewrite, state what the legacy pattern got wrong — the diagnosis is as valuable as the fix.
- **Promote and demote.** Principles graduate from `Candidates` → `Principles` → `Laws` as they prove themselves. Demote anything that turns out to be situational rather than universal.

---

## The Five Prime Virtues

These are the fixed points around which the methodology orbits. Every principle below should serve at least one of these; if it doesn't, it doesn't belong.

1. **Maintainability** — Code is read far more than written. Every change leaves the codebase easier to modify.
2. **Extensibility** — New vehicles, weapons, sensors, effects slot in by *adding* code, not rewriting it.
3. **Reliability** — Predictable across runs, frame rates, hardware, and load. Determinism where it matters.
4. **Testability** — Simulation logic must be reachable by automated tests. Untestable code is a design defect.
5. **Performance** — Real-time, high-fidelity sim is the target. Profile, then optimize. Don't be carelessly wasteful by default.

---

## Laws (universal, non-negotiable)

*Principles graduate here when they've survived multiple systems and at least one painful counter-example.*

- *(none yet — populated as the methodology hardens)*

---

## Principles (working set)

*Concrete rules grounded in real cases. Subject to revision but currently load-bearing.*

### Configuration & data

- **P1. Tuning values live in ScriptableObjects, not in code.** Weapon profiles, sensor specs, vehicle stats, AI parameters, environmental constants — all SO-driven. No magic numbers, no `[SerializeField]` literals scattered on MonoBehaviours.
  - *Why:* Decouples tuning from compile. Lets designers and you iterate without rebuilding. Makes A/B and regression testing tractable.
  - *Origin:* Established as project-wide standard; mirrors the `Guided Fury/ScriptableObjects/` layout.

### Time & determinism

- **P2. Physics, ballistics, and sensor logic run on `FixedUpdate`.** Frame-rate independent. Two runs at different framerates must produce the same engagement outcome (given the same seeds).
  - *Why:* Sim integrity. A weapon system whose behavior depends on framerate is not a simulation, it's an animation.
  - *Origin:* Project-wide standard; will be tested via `RH Testing Suite` once a framerate-variance test exists.

### Module structure

- **P3. New systems follow the `Core / Modules / ScriptableObjects / Prefabs / Examples` layout.**
  - *Why:* Predictable navigation. The Core/Modules split forces a thinking pass on what's stable vs. what varies. Examples keeps the system self-demonstrating.
  - *Origin:* Mirrors `Assets/Guided Fury/`.

### Performance

- **P4. Burst / Jobs / ECS go on the hot paths only.** Many-entity physics, sensor sweeps, ballistic traces, swarm AI. Don't force DOTS on small systems — the complexity tax isn't worth it.
  - *Why:* Maintainability vs. performance balance. DOTS-everywhere is a different methodology with different costs.
  - *Origin:* Project-wide standard; not yet stress-tested against a real high-N system.

### Boundaries

- **P5. Vendor code is read-only by default.** Wrap, extend, or compose. Modify only with explicit permission.
  - *Why:* Upgradability and blast-radius control. A vendor patch that gets overwritten on the next package update is a silent regression.
  - *Origin:* Project rule; vendors include RealisticCarControllerV3, NWH, Obi, RootMotion, SensorToolkit, HurricaneVR, Cesium, FlightSimLite.

### Architecture

- **P6. Simulation core is plain C#; `MonoBehaviour` is a thin adapter.** Integration, guidance, control, and state-evolution code lives in pure C# classes/structs — no `MonoBehaviour`, no `Time.deltaTime`, no Unity lifecycle dependency. `MonoBehaviour` exists only to (a) pump the simulation each `FixedUpdate`, (b) surface state to Unity transforms / rigidbodies, and (c) receive Inspector configuration.
  - *Why:* Three benefits in one rule. (1) **Testability** — pure C# is unit-testable in isolation without spinning up a scene. (2) **LOD/integrator swapping** — if state and integration are tangled into MonoBehaviour lifecycle, you can't cleanly swap implementations. (3) **DOTS migration path** — a Burst-friendly struct-based core can later be hoisted into Jobs/ECS without rewriting the math.
  - *Origin:* Promoted from candidate C1 when Guided Fury's LOD-swappable architecture made the rule non-negotiable. Reference implementation: `Assets/Guided Fury/Core/Missile/MissileEntity.cs` (pure C#) + `MissileBehaviour.cs` (adapter).

- **P7. Determinism via fixed-seed RNG injected through SO config.** Anything in the sim core that needs randomness (seeker noise, INS drift, fuze jitter, dispersion patterns) takes its RNG as a constructor/field parameter, not from `UnityEngine.Random` or `System.Random` with default seed. Seeds come from the `MissileProfileSO` (or equivalent per-system SO).
  - *Why:* Reproducibility. Two runs with the same scenario and seed must produce the same outcome — required for replays, AAR, regression tests, and credible balancing. Hidden global RNG defeats all of these.
  - *Origin:* Promoted from candidate C2 when Guided Fury L4+ planning surfaced the need (gimbal noise, glint, seeker jitter). Will be exercised first by L5 HWIL.

---

## Patterns (recurring shapes worth naming)

*Reusable structural patterns observed across systems. Less mandatory than Principles — more like vocabulary.*

- **Pat1. SO-Profile + Pure-Core + MB-Adapter.** Three-layer shape for any simulation system:
  1. **`XxxProfileSO`** — authored asset holding tuning data for the system. May contain runtime-struct equivalents (`XxxProfileData`) that are unmanaged-friendly.
  2. **`XxxEntity`** — pure C# class/struct holding state + behavior. No Unity references. The simulation lives here.
  3. **`XxxBehaviour`** — `MonoBehaviour` adapter. Owns the entity. Pumps it each `FixedUpdate`. Surfaces state to the Unity scene.
  - *Where exercised:* Guided Fury (`MissileProfileSO` + `MissileEntity` + `MissileBehaviour`). To be replicated for sensors, vehicles, AI agents.

---

## Anti-patterns (things we've learned not to do)

*Catalogued from legacy code or mistakes. Each entry: what it looks like, why it fails, what to do instead.*

- **AP1. Reflection-based SO → component field setting.** Setting private fields by string name via `Type.GetField("fieldName", ...)`.
  - *Where seen:* Legacy `Assets/_Archive/Guided Fury_legacy/ScriptableObjects/MissileConfigSO.cs:237–251`.
  - *Why it fails:* Silent breakage on rename/refactor (no compile error). Bypasses encapsulation. Untestable in isolation (depends on private field layout). Makes the SO's contract invisible to any tool, including the IDE.
  - *Do instead:* Public `Apply(in ProfileData data)` methods on the target, or pass an unmanaged `ProfileData` struct directly into the entity constructor. Names verified at compile time.

- **AP2. Physics simulation in `Update()`.** Calling `Rigidbody.AddForce()`, integrating velocity, or stepping aero from `Update()` instead of `FixedUpdate()`.
  - *Where seen:* Legacy `Assets/_Archive/Guided Fury_legacy/Core/MissileBase.cs:79–112` (calls `missilePhysics.UpdatePhysics()` from `Update`) and `MissilePhysics.cs:85–142` (uses `Time.deltaTime` while applying rigidbody forces).
  - *Why it fails:* Non-deterministic across framerates. Fights the PhysX solver, which steps separately on `FixedUpdate`. Engagement outcomes change with display refresh rate. Untestable, unreplayable, unbalanceable.
  - *Do instead:* All simulation-step work happens in `FixedUpdate()` with `Time.fixedDeltaTime`. P2 is the rule; this anti-pattern is the diagnosis.

- **AP3. Module auto-discovery by component scan.** Discovering "modules" via `GetComponents<MonoBehaviour>()` and filtering by interface.
  - *Where seen:* Legacy `Assets/_Archive/Guided Fury_legacy/Core/MissileBase.cs:308–320`.
  - *Why it fails:* Magic — what's attached affects behavior implicitly. Hard to reason about from the code alone (you must inspect the prefab). Order of attachment becomes a hidden dependency. Hard to test (you need a fully-built GameObject). Easy to break by accidentally adding an unrelated MonoBehaviour that implements the interface.
  - *Do instead:* Explicit composition. The profile SO (or entity constructor) names exactly which modules are active. If runtime variability is needed, expose a typed registry, not a scan.

- **AP4. Cross-component reach-around via public side-channel.** Component A calls a public method on component B that exists solely to let A poke B's state.
  - *Where seen:* Legacy `MissileGuidance.cs:141–143` calls `missileBase.GetPhysics().OnGuidanceUpdate(targetPosition)`.
  - *Why it fails:* The "side-channel" method has no real owner — it's neither input (no interface contract) nor event (no subscription). It pretends to be encapsulation while leaking it. Refactor blast-radius is huge because the call is invisible to the type system.
  - *Do instead:* A clean interface: guidance produces a `Command` struct; the integrator consumes it. The contract is the struct, owned by neither side.

- **AP6. Runtime `AddComponent<Rigidbody>()` on game objects with arbitrary provenance.** Adding a `Rigidbody` (and trigger `Collider`) to a missile / projectile / similar at runtime to enable physics-trigger-based detection.
  - *Where seen:* First-draft `MissileBehaviour.EnsureFuzeTrigger` set up a kinematic Rigidbody + trigger SphereCollider in `Launch()`. Result: intermittent `AddComponent<Rigidbody>()` returning null on certain prefab paths → `NullReferenceException`, then cascading `Rigidbody.WritePose` / `Invalid AABB` errors as the half-initialized body tried to sync to a transform written by the integrator.
  - *Why it fails:* `[DisallowMultipleComponent]` on `Rigidbody` plus Unity's variable AddComponent behavior across prefab / runtime / Editor states produces null returns that defensive null-checks alone don't catch (because the failure mode includes "added but in a state where pose-sync explodes"). The whole approach yokes physics-engine internals to your detection logic.
  - *Do instead:* `Physics.OverlapSphereNonAlloc` from `FixedUpdate`. Simpler, no component lifecycle, deterministic, allocation-free with a static buffer, ignores triggers via `QueryTriggerInteraction.Ignore`. Reference impl: `Assets/Guided Fury/Core/Missile/MissileBehaviour.cs:CheckProximityFuze`.

- **AP5. Hardcoded `Shader.Find("Standard")` in pipeline-agnostic code.** Asset-creation code that always reaches for the legacy built-in `Standard` shader.
  - *Where seen:* First-draft `MissileRangeSceneBuilder` produced magenta materials in this URP project before being corrected.
  - *Why it fails:* `Shader.Find` returns null when the shader doesn't exist in the active render pipeline; Unity falls back to its magenta error shader. Worse, even after fixing the shader, `material.color` writes `_Color` only — URP/HDRP lit shaders use `_BaseColor`, so colors silently fail to apply. Every modern Unity project ships URP or HDRP; written this way, the bug surfaces immediately for everyone but the original author.
  - *Do instead:* Pick the shader from `GraphicsSettings.currentRenderPipeline.defaultMaterial.shader` with name-based fallbacks (URP/Lit → HDRP/Lit → Standard → Unlit/Color). Write color via `HasProperty("_BaseColor") ? SetColor(_BaseColor) : SetColor(_Color)` — preferably both, since `HasProperty` is harmless on the wrong shader. Reference helper: `Assets/Guided Fury/Examples/Scripts/RangeMaterials.cs`.

---

## Candidates (under evaluation)

*Ideas that might become Principles. Not yet load-bearing. Promote when grounded in a real case.*

- **C3. Scene-as-test-fixture.** Use minimal Examples scenes as both demos and integration-test fixtures. Re-evaluate once `RH Testing Suite` integrates with scene loading.
- **C6. Unit tests are colocated with the system; integration/scenario tests are centralized in `RH Testing Suite`.** EditMode tests that bind tightly to integrator math, guidance laws, etc. live in `Assets/Xxx/Tests/Editor/` next to the implementation. Cross-system / scenario / replay tests go in `RH Testing Suite` so they're centrally discoverable. Promote when a second system adopts the pattern.
- **C7. Profile struct + SO-baked-once.** The SO authors data; `Bake()` produces an unmanaged struct that the entity carries for its lifetime. Live edits to the SO don't retroactively change in-flight instances. Promote when a second system (sensor, vehicle) adopts the pattern.
- **C4. `XxxState` struct as universal interchange.** For any system with multiple fidelity levels or integrators, a single unmanaged state struct serves as the lingua franca — every integrator reads from it, mutates it, writes back. Round-trip-safe across LODs even if some fields are unused at the lower tier. Promote when the L0→L4 ladder in Guided Fury has fully validated the round-trip.
- **C5. Replaced legacy code goes to `Assets/_Archive/`, never deleted outright until cutover is proven.** Original folder name + `_legacy` suffix; original `.meta` preserved so GUIDs survive. Lets you reference old behavior during rebuild and gives a clean rollback path. Promote after one full cutover proves the workflow.

---

## Open questions

*Methodology decisions we haven't made yet. Re-visit periodically.*

- How should cross-system events flow? (Event bus? Direct refs via SO? UnityEvents? Custom messaging?)
- Where does AI/behavior config live when it spans multiple systems (e.g., a destroyer's engagement doctrine touches sensors + weapons + helm)?
- What's the test pyramid for a sim? (Unit / scene-integration / scenario-replay — ratios?)
- How do we version SOs so a tuning change doesn't silently break saved scenarios?

---

## Changelog

- **2026-05-16** — Initial draft. Established Prime Virtues, P1–P5, and seeded Candidates C1–C3.
- **2026-05-16** — Guided Fury rebuild kickoff. Promoted C1→P6 (plain-C# core + MB adapter) and C2→P7 (deterministic RNG via SO). Added Pat1 (SO-Profile + Pure-Core + MB-Adapter). Catalogued AP1–AP4 diagnosed from `_Archive/Guided Fury_legacy/`. Seeded C4 (universal state struct) and C5 (`_Archive/` convention).
- **2026-05-16** — Guided Fury Phase 2 complete (L0 + L1 + guidance abstraction + EditMode tests). Seeded C6 (test colocation) and C7 (profile struct + bake-once). P6 reference implementation exercised by 18 EditMode tests that construct entities directly without a scene — payoff of pure-C# core demonstrated.
- **2026-05-17** — Phase 2.5 test range tooling. Generated scene with markers/targets/hittable boxes via editor menu command. Catalogued AP5 (hardcoded `Shader.Find("Standard")` in pipeline-agnostic code — magenta-fallback footgun under URP/HDRP).
- **2026-05-17** — Guided Fury Phase 3 complete (L2 rate-limited 3DOF + seeker abstraction + cone seeker). 30 EditMode tests across the suite. `ITargetSource` gained an `Update(in MissileState, float dt)` method so seekers (stateful) and truth sources (mostly stateless) live behind the same contract — no entity-side knowledge of which kind is plugged in.
- **2026-05-17** — Guided Fury Phase 4 complete (L3 pseudo-6DOF). Full rigid-body orientation evolved via quaternion ω-integration. Body-axis thrust, AoA-induced lift with linear-then-stall model, weather-vane restoring moment, inline rate autopilot mapping lateral-accel commands to commanded body rates. Autopilot stays inline at L3; L4 will extract it to an `IAutopilot` interface when multiple autopilots become useful. 37 EditMode tests total.
- **2026-05-17** — Guided Fury Phase 5 complete (test range polish). Added IMGUI HUD listing every active missile's telemetry; chase/overview camera controller with key toggles; LOD comparison runner firing one missile per LOD in color-coded salvo; per-missile trail renderers using `Unlit/Color` (pipeline-portable). HUD and camera are pure debug/diagnostic — zero gameplay coupling, scan-based discovery via `FindObjectsByType` at 4 Hz.

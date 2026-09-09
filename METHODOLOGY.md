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

- **AP8. Adopting a vendor prefab as-is when your system owns motion.** Using a vendor / legacy prefab (`ESSM Shell.prefab`, an aircraft, a vehicle) as the instance for a system whose adapter writes the transform every FixedUpdate, without neutralizing the vendor's own physics / scripts.
  - *Where seen:* User assigned `Assets/rim-162essm/ESSM Shell.prefab` as `missilePrefab` on `GuidedFury_TestRunner`. Prefab ships with a non-kinematic `Rigidbody` (`m_UseGravity: 1`) and a legacy `ESSMHomingMissile6DoF` script. On launch the missile fell straight down — PhysX gravity (and the legacy script) overrode the new `MissileBehaviour`'s kinematic transform writes.
  - *Why it fails:* P5 says vendor code is read-only by default, but it doesn't say "instantiate it as-is." When your adapter owns motion (FixedUpdate transform writes, P2/P6), any other component on the same GameObject that ALSO writes the transform or applies physics forces is a hidden competitor. Worst case: silent fight, where one system wins on some frames and loses on others, producing erratic motion the user can't trace.
  - *Do instead:* In the adapter's Launch / activation path, **actively neutralize foreign motion drivers** on the same GameObject:
    - Any `Rigidbody`: force `isKinematic = true`, `useGravity = false`, zero velocities.
    - Any sibling `MonoBehaviour` outside your system's namespace: disable it on launch (or warn loudly).
    - The neutralization is opt-out via a serialized bool so a user who genuinely wants the vendor physics to coexist can disable it.
  - *Reference impl:* `Assets/Guided Fury/Core/Missile/MissileBehaviour.cs:NeutralizeForeignComponents`.

- **AP10. Scale-blind `TransformPoint` offsets around vendor prefabs.** Computing a follow/mount/spawn position as `foreign.TransformPoint(localOffset)` when the foreign object is a vendor prefab whose authored scale you don't control.
  - *Where seen:* `MissileCameraController` chase view. `chaseOffset (0, 4, -15)` was applied via `TransformPoint` on the spawned `ESSM Shell` prefab, whose root scale is `(65, 65, 100)`. The 15 m offset became ~1.5 km: the camera slingshotted 1300 m behind the launcher on fire and orbited the whole flight from a kilometer and a half away. Combined with AP8's embedded "Missile Cam" (far clip 1000) hijacking the display, the user-visible symptom was "camera doesn't track the missile; it disappears after a moment."
  - *Why it fails:* `TransformPoint` applies position, rotation, **and lossyScale**. On your own rigs scale is usually 1 and the bug never fires. Vendor art is routinely authored at arbitrary scale to fit some other engine or export pipeline, so any camera rig, hardpoint, muzzle, or effect anchor that offsets "in the vendor object's space" inherits that scale silently. The failure is proportional to the vendor's scale factor, so it looks like a physics or smoothing bug, not a transform bug — expensive to diagnose.
  - *Do instead:* For offsets around objects you don't author, compose position and rotation only: `pos = foreign.position + foreign.rotation * localOffset`. Reserve `TransformPoint` for hierarchies whose scale you own. Corollary of AP8: adopting a vendor prefab means neutralizing not just its motion drivers but also its *metric assumptions*.
  - *Reference impl:* `Assets/Guided Fury/Examples/Scripts/MissileCameraController.cs` (chase pose).

- **AP7. State-machine entry side-effects in per-step case bodies.** Setting state that should fire "on entry to stage X" inside the `case X:` body of a `switch` driven from per-step Step logic.
  - *Where seen:* First-draft `CatapultCycle.Step` set `cat.JbdRaised = true` inside the `case CatapultStage.Tensioned:` body. When the Spotted case detected timeout and called `Transition(Tensioned)`, the switch exited with `break` — the Tensioned case body did not run in that same step. Result: a one-step lag on every transition (`JbdRaised` was false for one frame after entering Tensioned, true for one frame after entering Retracting). The bug surfaced in `JBD_RaisedDuringTensionedThroughFiring_LoweredDuringRetracting` because the test asserted around the transition boundary.
  - *Why it fails:* "Entry" and "per-step ongoing" are different semantic operations. Conflating them in the same `case` body means every stage-entry side-effect is delayed by exactly one Step. For long-duration stages the lag is invisible; for short stages, near-boundary observers, or systems that gate on the flag (visuals, sounds, animator triggers) it manifests as a one-frame flicker every transition. Defensive workarounds (assert with margin in tests, add edge-detection guards in downstream consumers) treat the symptom, not the cause.
  - *Do instead:* Centralise transitions through a single `Transition(ref state, NextStage)` helper that:
    1. Sets the new stage and resets the stage timer.
    2. Calls an `OnEnter(ref state, NextStage)` hook where entry side-effects live.
   Per-step case bodies then only handle *ongoing* logic — timer-based stage completion, physics integration, motion. Reference impl: `Assets/Carrier Ops/Core/Catapult/CatapultCycle.cs:OnEnter`.

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
- **2026-05-17** — CVN-78 scene cleanup pass landed (items 1–8, 15, 27 from audit). Removed `_RCCSceneManager` + broken VR hierarchy, reorganized root into 6 logical parents via editor menu, renamed `Cat 1 → Waypoint_Approach`, enabled HDR + TAA on Main Camera, spatialized F-18 audio, refactored CarrierController Animator triggers to cached `StringToHash` hashes, bumped Sun shadow resolution. Catalogued anti-pattern AP6 (runtime `AddComponent<Rigidbody>()` on arbitrary GameObjects → magenta-fallback-style footgun).
- **2026-05-17** — Carrier Ops module founded as the second system to follow Pat1 (SO-Profile + Pure-Core + MB-Adapter), validating the pattern across a different domain (ship motion + cycle state machines, no missiles). Items 9–14, 18, 19 from the CVN-78 audit landed in one phase: `CarrierProfileSO` + bake, deterministic sum-of-sines motion (replaces `Mathf.Sin(Time.time)` sway — AP2-adjacent), `ShipKinematics`, catapult cycle state machine (Idle → Spotted → Tensioned → Ready → Firing → Retracting), JBD slaving, elevator cycle, `CarrierLaunchControlPanel`, migration helper. 15+ EditMode tests covering motion determinism, catapult timing, ship kinematics. Legacy `CarrierController.cs` left in place until prefab migration is verified (per C5 archive policy).
- **2026-05-17** — Catalogued AP7 (state-machine entry side-effects in per-step case bodies → one-step lag on every transition). Diagnosed via a flaky test on `CatapultCycle.JBD_Raised…` that turned out to be exposing a real semantic bug, not a knife-edge timing artifact. Fix: centralised `OnEnter` hook called from a single `Transition` helper. Pattern applies to any system using a Step-driven `switch` state machine — relevant beyond catapults.
- **2026-05-17** — Carrier Ops Phase 2 complete (items 16 + 17 from CVN-78 audit). Added FLOLS pure-geometry model (`FlolsModel.Sample`), arresting-gear state machine (4 wires, AP7-mitigated with `OnEnter`), `IRecoveringAircraft` contract for hook-side, `TailhookHook` companion that gives any aircraft (AerialArcade F-18 included) the recovery interface without modifying it. Carrier now does **complete deck cycle** — launch + recovery. `CarrierEntity` gained a small recovery-registry (int → IRecoveringAircraft) so wire state can stay unmanaged. 12+ new EditMode tests covering FLOLS geometry and wire cycle. Pat1 (SO-Profile + Pure-Core + MB-Adapter) now load-bearing across 2 systems / 3 subsystems (Guided Fury entity, Carrier entity, Carrier recovery) — candidate for promotion to Principle next session.
- **2026-05-17** — CVN-78 prefab migration tooling: rewrote `CVN78MigrationHelper` from a checklist-printing stub into an actual `PrefabUtility.LoadPrefabContents` + reflection-based extractor that swaps the legacy `CarrierController` for `CarrierBehaviour`, configures slots, and creates `_Generated_*` child transforms for the things the legacy didn't have. Second menu command (`Archive Legacy CarrierController`) handles the post-verification archive step, refusing to run while the prefab still references the legacy script. Idempotent.
- **2026-05-17** — Guided Fury Phase 5 polish complete. Added `SalvoRunner` (same-LOD staggered salvo), `LodComparisonStats` (per-LOD telemetry overlay showing peak speed / miss distance / fuel / outcome), `EngagementScenarios` (Stationary/Head-on/Crossing/Tail-chase preset picker integrated into the control panel), `LodTrailColors` shared palette (HUD + trails + stats agree on the LOD → color mapping). HUD's LOD column is now tinted by LOD so you can correlate world-view trails with HUD rows at a glance. Scene builder updated to spawn all new components automatically.
- **2026-05-17** — Catalogued AP8 (adopting a vendor prefab as-is when your system owns motion). Diagnosed via the ESSM Shell prefab "falls straight down" symptom — vendor Rigidbody + legacy missile script were competing with MissileBehaviour's transform writes. Fix: `NeutralizeForeignComponents()` in Launch — forces Rigidbody kinematic + disables foreign MonoBehaviours. Same pattern will apply to any adapter-based system that takes vendor prefabs (carrier with AerialArcade F-18, vehicle with RCC car).
- **2026-05-17** — Guided Fury Phase 6 complete: L4 full-aero 6DOF. **Three new orthogonal subsystems** materialized as pluggable interfaces — `IAeroModel` (`SimpleAeroModel`, `TabulatedAeroModel` with Mach-aware AnimationCurves), `IAutopilot` (`SimpleRateAutopilot` extracted from L3's inline logic, `SurfaceDeflectionAutopilot` for two-loop fin control), and `IThrustModel` (`ConstantBoostThrustModel`, `BoostSustainThrustModel` for tactical motor profiles). MissileState gains `PitchDeflectionRad` / `YawDeflectionRad` for rate-limited fin tracking. `HighestImplementedLod` bumped to L4 so the auto-downgrade path now respects the new tier. Pat1 (SO-Profile + Pure-Core + MB-Adapter) is now load-bearing across 4 systems / 5 subsystems (Guided Fury entity, Carrier entity, Carrier recovery, plus the three new "model" subsystems all follow the interface + factory + kind-enum shape) — promotion to Principle next session. New candidate Pat3 (working title): "Orthogonal model subsystems" — when a complex system has multiple independent axes of variation (here: aero × autopilot × thrust), each axis gets its own interface + factory + profile-kind-enum, composed at construction. Avoids the combinatorial explosion of "L4ESSMAeroAutopilotBoostSustain" specialized classes.
- **2026-05-17** — First authored real missile: RIM-162 ESSM at L4. Programmatic asset builder via `EssmProfileBuilder` editor command. Reference values + Mach-dependent aero curves baked from publicly-available specs (mass, dimensions, motor burn time, top speed, range, g-rating) plus slender-body theory norms for coefficients. Validates the L4 stack against a concrete known weapon — the test range can now fire a "real" SAM, not just a generic profile. Methodology candidate Pat4 (working title): **"Authored-missile builder as an editor command, not a .asset YAML."** The asset is generated programmatically with real-world reference data as code; rerunning resets to authored defaults; manual tuning in the Inspector is preserved across reruns only when the user opts out. Beats hand-authoring `.asset` YAML for several reasons: (1) the reference data is auditable as source code, with citation comments; (2) curves are explicit programmatic objects, not opaque serialized blobs; (3) running the command on a fresh checkout produces a known-good baseline. Promote when a second real missile (Sidewinder, AMRAAM) follows the same pattern.
- **2026-05-17** — Guided Fury Phase 5 complete (test range polish). Added IMGUI HUD listing every active missile's telemetry; chase/overview camera controller with key toggles; LOD comparison runner firing one missile per LOD in color-coded salvo; per-missile trail renderers using `Unlit/Color` (pipeline-portable).
- **2026-05-23** — Catalogued AP9 (passing `Vector3.up` directly to `Quaternion.LookRotation` for an orientation that tracks a free-flying velocity vector). When the tracked vector swings toward ±world Y — vertical climb, steep dive, or any maneuver passing through it — forward becomes parallel to up, `LookRotation` returns an arbitrary frame-flipping roll, and visually the body tumbles. Fix: a `StableLookRotation(forward, currentOrientation)` helper that carries the previous up vector forward and falls back to perpendicular axes only when parallel. Applies to L1/L2 in Guided Fury today; same trap will hit any kinematic "point camera/body along velocity" code in the project (e.g. tracking cameras, missile-cam chases, ballistic projectiles). HUD and camera are pure debug/diagnostic — zero gameplay coupling, scan-based discovery via `FindObjectsByType` at 4 Hz.
- **2026-09-08** — Diagnosed and fixed the "chase camera loses the missile" report; three stacked causes, two new methodology entries. (1) AP8 extension: the ESSM Shell prefab carries an embedded, enabled `Missile Cam` (far clip 1000) that became the top rendered camera on every launch and died with the missile — `NeutralizeForeignComponents` now disables embedded Cameras and AudioListeners in children, and zeroes rigidbody velocities *before* flipping kinematic (PhysX ignores the write and warns otherwise). (2) Catalogued AP10: scale-blind `TransformPoint` chase offset × the prefab's (65, 65, 100) root scale put the chase camera 1.5 km off station; fixed with rotation-only offset composition plus missile-velocity feed-forward (cancels SmoothDamp's ~v×smoothTime steady-state lag, ~200 m at Mach 4) and a stale-SmoothDamp-velocity reset on chase-target change. (3) Applied AP9's `StableLookRotation` to the chase camera (vertical boost roll-flip). Far clip raised to 30 km on both range cameras + scene builder. Verified with frame-sampled flight traces: camera holds 4–18 m behind the missile, on-screen centered, full 3.9 s intercept.
- **2026-09-08** — ESSM asset normalized + detonation effects. (1) The `ESSM Shell` prefab was authored at (65, 65, 100) root scale — a 364 m missile. Normalized in-editor by folding root scale × shrink factor into the direct children's TRS matrices (numeric decomposition with a shear check), preserving the exact authored look at the real RIM-162 length of 3.66 m with root scale 1. Rule of thumb: fix vendor scale at the *asset*, once — every compensating hack downstream (camera offsets, HUD sizes, fuze radii) then dies of natural causes. (2) Detonation VFX wired through the existing `MissileProfileSO` slots (aerial = DAVFX HDRP air burst, ground = HD_Naval_Explosion; both plain Inspector fields, swap at will) — the altitude-threshold selection worked untouched. (3) New physical blast: `blastRadiusM` / `blastImpulseNs` / `blastUpwardsModifier` on the profile (0 radius = off, so existing profiles are unaffected), applied in `MissileBehaviour.ApplyBlastImpulse` via `OverlapSphereNonAlloc` + per-body dedup + `AddExplosionForce` (linear falloff, impulse mode). Verified: point-blank detonation threw eight 100 kg crates at ~120 m/s (matches impulse math); a detonation 38 m out correctly moved nothing. Observed during testing, not yet fixed: L4 vs a stationary ground target overflies at ~40 m altitude and detonates ~150 m past it — terminal guidance / proximity-fuze tuning is a follow-up.
- **2026-09-08** — Fixed-step visual interpolation (candidate principle, working title Pat5: "Sim on FixedUpdate, render interpolated"). User report: at 0.1x time scale the missile "stutters through space." Cause: P2-compliant FixedUpdate simulation writes the transform once per fixed step; at 0.1x a 20 ms fixed step spans 200 ms of real time while rendering continues at full frame rate — the missile teleports ~4 m every ~13 frames. Fix: keep the pose pair from the last two fixed steps and, in Update, render `Lerp/Slerp(prev, curr, (Time.time - Time.fixedTime) / fixedDeltaTime)`. Simulation state, guidance, and the fuze are untouched (they read `entity.State`, never the transform), so determinism holds; cost is one fixed step of visual latency; opt-out via a serialized bool. Same pattern applied to `EngagementScenarios.ConstantVelocityMover` with `[DefaultExecutionOrder(-50)]` so its FixedUpdate restores the exact sim pose before any missile's FixedUpdate samples the target transform. Verified at 0.1x: rendered position advanced on 553/553 render frames (~0.3 m even steps, max 0.75 m), vs frozen-then-jump before. Generalizes to every kinematic FixedUpdate-driven mover in the project — ships, aircraft, ground vehicles; promote to Principle once a second system adopts it.

# RH Radar Suite — Roadmap

Authored 2026-09-08 by the **RH Radar Panel** (see `.claude/skills/radar-panel-review/`):
Merrill Skolnik (chair, detection physics) · David K. Barton (analysis & accuracy) ·
George W. Stimson (airborne & visualization) · Eli Brookner (tracking & arrays) ·
RADM Wayne E. Meyer (program chair, integration).

**Goal (owner's statement):** usable radar modules that can be easily added to any
asset — ground radar, aerial radar, naval radar — with gizmos that let any user
visualize and diagnose what a radar is doing.

**Ledger rules (same as Guided Fury):** phases MAY be reordered for dependency.
Unchecked items MUST NOT be deleted — mark `~~skipped~~` + reason. Completed items
get a date. New findings append to the ledger at the bottom.

**Methodology alignment:** every phase is reviewed against `METHODOLOGY.md` —
P1 (SO-driven tuning), P2 (FixedUpdate sensor logic), P6 (pure-C# core / thin
MB adapter), P7 (seeded determinism), Pat1 (SO-Profile + Pure-Core + MB-Adapter).

---

## Panel review of the existing suite (2026-09-08)

The March 2025 commit is a sound skeleton — clean module interface, event-driven
contacts, LOD concept, an honest first visualizer. The panel's findings on the
current implementation, attributed and file-specific:

- **[Skolnik] The radar equation isn't one.** `BasicRadarModule.CalculateSignalStrength`
  is `1/R⁴ × power × RCS × 1e9, clamped 0–1` — a magic scale constant in place of
  physical terms. No units, no noise floor, no way to reason about tradeoffs.
- **[Skolnik] The Earth is missing.** No raycast/LOS check anywhere in `LOD/` — every
  radar sees through terrain, buildings, and the horizon. Cheapest realism win available.
- **[Skolnik] RCS dynamic range.** `RadarSignature.CrossSection` is a 0.01–10 linear
  multiplier; real RCS spans ~five orders of magnitude and is quoted in dBsm.
- **[Barton] Inconsistent error model.** `AddRangeInaccuracy` is applied only when a
  contact is first created (`BasicRadarModule.cs:198-204`); every subsequent
  `contact.Update` uses truth position. Noise that remembers whether it saw you
  before is not noise.
- **[Barton] Errors in the wrong frame.** `PassiveDetectionModule.cs:208` scatters
  error with `Random.insideUnitSphere` — isotropic Cartesian error. Radar errors
  live per measurement axis (range σ, azimuth σ, elevation σ) and scale with SNR.
- **[Barton] Unseeded RNG.** All modules use `UnityEngine.Random` — violates P7;
  no run is reproducible.
- **[Stimson] World-frame bug — fatal on any moving platform.** Sector checks use
  `Vector3.SignedAngle(Vector3.forward, …)` (`BasicRadarModule.cs:250`,
  `DopplerRadarModule.cs:290`) and sweep directions build from world
  `Vector3.forward`. A fighter that banks 90° keeps a north-pointing radar. All
  angles must be body-relative.
- **[Stimson] The sweep is cosmetic.** In full-rotation mode `OverlapSphere` detects
  360° every scan regardless of where the beam points; `beamWidth` gates nothing.
  The gizmo shows a rotating beam the detection logic doesn't have.
- **[Stimson] Coverage gizmo overpromises.** A wire sphere of radius maxRange implies
  hemispheric coverage; no elevation limits exist in model or drawing.
- **[Brookner] Re-detection, not tracking.** Contacts are keyed by GameObject and
  dropped the instant one scan misses (`PerformRadarScan` lost-target sweep). No
  tentative/confirmed/coast lifecycle, no smoothing, no gating. Fine for LOD1–2 if
  the debt is named — it is now named.
- **[Brookner] String-typed parameter API.** `SetParameter("beamWidth", value)` is
  AP1's cousin — no compile-time safety, silent typo failures. SO profiles (P1)
  make it unnecessary.
- **[Meyer] Attachability is broken in the tool that exists.** `InitializeLODModules`
  (`RadarSuiteController.cs:232-236`) unconditionally `AddComponent`s all five
  modules at runtime — duplicating any module the user configured in the Inspector
  and discarding their tuning. The advertised workflow ("add controller, add
  modules, configure") fights the code.
- **[Meyer] Nothing is tested and nothing is demonstrable.** Zero EditMode tests;
  `Examples/` and `Prefabs/` are READMEs describing assets that don't exist. No
  phase below may close without its test and its demo.
- **[Meyer] No consumer contract.** Guided Fury seekers, ship combat systems, and AI
  can't yet ask this radar a question ("best track bearing 045?"). The suite emits
  events but has no queryable track picture.
- **[Barton, addendum found during Phase 1] Contacts measured from the camera.**
  `RadarContact.Update` computed Range/Azimuth/Elevation from `Camera.main.transform`
  — every readout was camera-relative, not radar-relative. Fixed 2026-09-08
  (`RadarOrigin` passed by every module on every update).
- **[Skolnik, addendum, quantified] The magic constant made the range knob a lie.**
  With the hard-coded `1e9` normalization, a power-1 / RCS-1 radar crossed the 0.1
  threshold only inside ~316 m — nothing was ever detectable at the advertised
  5 km default. Fixed 2026-09-08 by `RadarMath.NominalSignalScale`: profiles
  calibrate the scale so a nominal target at max range sits at 4× threshold
  (interim abstraction; the real equation remains Phase 2).

**Consensus:** keep the architecture (controller + LOD modules + signature + events),
fix usability and truth-in-visualization first (that is the owner's stated goal),
then make the physics honest, then earn real tracking. **Disagreement, recorded:**
Skolnik wanted LOS/horizon in Phase 1 ("it's one raycast"); Meyer ruled it Phase 2 —
Phase 1 ships the attach-and-see experience, and a raycast that isn't tested against
terrain cases isn't a raycast. Barton sided with Meyer; noted for reversal if Phase 1
finishes early.

---

## Phase 1 — Fieldable Radar Kit (usability + gizmos) ← CURRENT

*Meyer's acceptance: select any GameObject → one menu action → a configured,
running radar with legible gizmos. Works identically on a parked truck and a
moving aircraft.*

- [x] **RadarProfileSO** (P1/Pat1) — DONE 2026-09-08. `ScriptableObjects/RadarProfileSO.cs`:
  role enum, range/power/layers/threshold, beam + rotation rpm + sector
  (body-relative), elevation min/max (stored now, enforced Phase 2), timing,
  accuracy, default LOD. `ApplyTo(controller)` → typed `ApplyProfile` on the
  controller and every module — no string parameters.
- [x] **Built-in profile presets** — DONE 2026-09-08. `Editor/RadarKit.cs`
  (Guided Fury asset-builder pattern): GroundSearch, AirSearch, NavalSurfaceSearch,
  FireControl, AirborneIntercept created on demand under
  `RH Navy Sims > Radar Suite > Create Default Profiles`
  (assets land in `ScriptableObjects/Profiles/`).
- [x] **One-click attach** — DONE 2026-09-08. Hierarchy context menu
  `GameObject > RH Navy Sims > Radar Suite > Add <role> Radar`: controller +
  default-LOD module + diagnostics, profile applied, Undo-aware, idempotent.
- [x] **Fix duplicate-module initialization** — DONE 2026-09-08.
  `InitializeLODModules` reuses existing components (`GetOrCreateModule`);
  Inspector tuning survives; controller tracks which modules it created.
- [x] **Body-relative angles** — DONE 2026-09-08. `Core/RadarMath.cs` (pure
  static, P6 seed): `FlatForward` / `BodyRelativeBearing` / `BodyDirection` /
  `IsAngleInSector` (DeltaAngle-based, wrap-safe). Basic, Doppler, and 3D
  modules all converted; aerial + turning platforms now scan their own frame.
- [x] **Diagnostic gizmo suite** — DONE 2026-09-08. `Utils/RadarDiagnostics.cs`,
  three toggleable layers: Coverage (range ring / sector arc / elevation wedge),
  Activity (sweep wedge at true beam width + fading trail), Measurements
  (measured marker + bearing line + velocity vector + truth-ghost link +
  lost-contact fade-out X), Handles labels (radar summary + per-contact
  RNG/BRG/EL/SIG).
- [x] **Editor-time preview** — DONE 2026-09-08. Coverage layer draws from the
  assigned profile outside play mode ("EDIT PREVIEW" label) so radars can be
  aimed before pressing play.
- [x] **Phase-1 example scene builder** — DONE 2026-09-08.
  `RH Navy Sims > Radar Suite > Build Example Scene` generates
  `Examples/RadarKitExample.unity`: ground tower, picket ship, orbiting patrol
  aircraft (CircleFlyer), five signature targets incl. a stealth drone, a noise
  jammer, and an orbiting bandit.
- [ ] **Phase-1 EditMode tests — written 2026-09-08, execution PENDING.**
  `Tests/Editor/RadarKitPhase1Tests.cs` (7 tests: sector wrap, body-relative
  bearing, FlatForward under pitch/roll, signal-scale calibration, ApplyProfile
  round-trip, idempotent attach, Inspector-module reuse). Whole suite compiles
  clean against the 6000.4.11f1 reference set (offline csc check), but the
  unity-mcp bridge was down this session — run `RHRadarSuite.Tests.Editor` in
  the Test Runner to close this item (Meyer's rule: not done until it runs).

## Phase 2 — Physical honesty (detection you can reason about)

*Skolnik's phase. Same architecture, real terms.*

- [ ] **Radar-equation core in pure C#** (P6): `Core/Detection/RadarEquation.cs` —
  SNR in dB from power, gain, wavelength band, RCS (dBsm), range, losses; unit
  tested against hand-computed cases.
- [ ] **Pd/Pfa detection threshold** with noise floor — replace the 0–1 clamped
  strength cliff; keep a simple Swerling-1-like fluctuation option.
- [ ] **Line-of-sight + radar horizon.** Physics raycast masking + 4/3-Earth
  horizon range check. Surface radars stop seeing sea-skimmers at 200 km.
- [ ] **Elevation coverage enforced** from profile (min/max elevation, scan
  pattern respects it); beam gating becomes real (detection only inside the
  beam/sector actually illuminated this scan).
- [ ] **RCS in dBsm** + signature aspect dependence (front/side/rear lobes, coarse).
- [ ] **Measurement-frame errors** (Barton): per-axis σ (range/az/el/range-rate)
  scaling with SNR, applied on *every* update, seeded RNG from profile (P7).
- [ ] **FixedUpdate sensor stepping** (P2) across all modules; scan timing from
  fixed time.
- [ ] **Phase-2 EditMode tests:** predicted-vs-simulated detection range for a
  published-style test case; horizon cutoff; error σ statistics over N seeded runs.

## Phase 3 — Tracks, not dots

*Brookner's phase: "α-β first — it's easy."*

- [ ] **Pure-C# α-β tracker** (`Core/Tracking/AlphaBetaFilter.cs`) with
  closed-form step-response unit test.
- [ ] **Track lifecycle:** tentative → confirmed (M-of-N) → coasting → dropped;
  per-state events; coast prediction through missed scans.
- [ ] **Gated association:** measurements associate to tracks by predicted-window
  gating, not GameObject identity (identity kept as LOD1–2 shortcut).
- [ ] **Track picture API** (Meyer): queryable `IReadOnlyList<RadarTrack>`,
  `BestTrack(bearing/sector)`, track quality metric — the consumer contract.
- [ ] **Kalman upgrade** (only after α-β baseline is beaten by a named need —
  maneuvering-target coast accuracy is the expected justification).
- [ ] **Track gizmos:** state-colored track symbols, history trails, gate ellipses,
  coast markers.

## Phase 4 — Environment & electronic warfare

- [ ] **Clutter model:** land/sea clutter floor by grazing geometry (coarse,
  Barton-style closed form, not per-cell); look-down penalty for airborne radar.
- [ ] **Weather attenuation** hooks (rain rate → dB/km by band).
- [ ] **Passive detection made honest:** emitters (`RadarEmitter` on transmitting
  radars) so LOD1 detects actual emissions with bearing-only + ambiguous range,
  instead of OverlapSphere omniscience.
- [ ] **Jamming expansion:** burn-through range from the equation (not a 0–1
  multiplier), noise vs deception behaviors affecting measurements differently,
  chaff as short-lived false-track spawner.
- [ ] **Multi-function array tier (LOD5 rework):** dwell scheduler splitting the
  time budget across search sectors + active tracks (Brookner's model) — replaces
  the current thin HighFidelityModule; SAR/ISAR imagery explicitly out of scope.

## Phase 5 — Combat-system integration & scale

*Meyer's phase: the fleet gets it.*

- [ ] **Guided Fury handoff:** radar track → `seekerMidcourseDatalink` midcourse
  updates; an SM-2/ESSM shot guided by an RH radar track end-to-end in the range.
- [ ] **Ship/vehicle/aircraft prefab integration:** radar-equipped CVN-78 island
  and picket examples; Prefabs/ README promises fulfilled.
- [ ] **IFF interrogation flow** (uses existing `IFFStatus`) + track classification
  surfaced to consumers.
- [ ] **Performance pass at scale:** N radars × M targets budget test; batching /
  Jobs only if the budget test demands it (P4).
- [ ] **Snapshot regression harness** mirroring Guided Fury's: canonical seeded
  scenario, golden detection/track statistics, CI gate.

---

## Known issues / tracked follow-ups

- [ ] All five LOD modules currently step in `Update()` with `Time.time` (AP2/P2
  violation) — scheduled Phase 2, listed here so it isn't forgotten if phases reorder.
- [ ] `RadarVisualizer.OnGUI` draws IMGUI labels at runtime for every contact —
  replace with Handles (editor) + optional world-space UI (runtime) during Phase 1
  gizmo work; keep runtime cost out of builds.
- [ ] `HighFidelityModule` (192 lines) is a stub relative to its LOD5 billing —
  Phase 4 replaces it; do not invest in it meanwhile.
- [ ] `RadarSuiteController.Cleanup` destroys modules it may not have created once
  the Phase 1 reuse fix lands — ownership must be tracked (created-by-controller flag).
- [ ] `Examples/README.md` and `Prefabs/README.md` describe assets that don't exist —
  Phase 1 and Phase 5 items close them; update the READMEs when they do.

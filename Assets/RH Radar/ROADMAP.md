# RH Radar — Routine Roadmap

> **STRUCK 2026-10-27 (SIX, General Quarters G61).** This greenfield plan was never built (no code; Phase 0
> unchecked). It duplicated RH Radar Suite, whose engine-free pure core (radar equation, detection, horizon, errors,
> clutter, tracking; 42 tests) already exists. That core is being split into `RHRadarSuite.Core` for General Quarters
> and the suite to share. Two radar plans side by side broke "one lock per decision". The folder is kept for the
> record; build nothing here. To restore it, remove this banner and re-open the decision in GQ's BLUEPRINT.

The autonomous routine reads this file each iteration, picks the topmost unchecked
item, executes it, runs the verification gate, commits on green, reverts on red.

**Instruction:** the routine MAY rearrange items below if a dependency reveals itself.
It MAY add new items at the end as it discovers them. It MUST NOT delete unchecked
items without first marking them `~~skipped~~` with a one-line reason.

---

## Phase 0 — Foundation

- [ ] **Archive legacy radar scripts.** Move `Assets/Scripts/Radar Scripts/` to
  `Assets/_Archive/Radar Scripts_legacy/` (preserve `.meta` files, Pat C5). Record
  before-state in `_Archive/<date>-radar-archive.md`.
- [ ] **State struct + profile struct.** `RH/Radar/Core/State/RadarState.cs` and
  `RadarProfileData.cs`. Unmanaged. Universal interchange across LODs (C4).
- [ ] **RadarProfileSO authoring asset.** ScriptableObject in `ScriptableObjects/`.
  Field-by-field `Bake()` to the struct (P1 + Pat1).
- [ ] **IPhysicsScanner contract.** Mirror of Guided Fury's IPhysicsIntegrator.
  Implementations in `Modules/Scanners/`.
- [ ] **RadarEntity (pure-C# core).** Holds the state, drives the scanner, emits tracks.
  Zero MonoBehaviour deps (P6).
- [ ] **RadarBehaviour MonoBehaviour adapter.** Pumps `Step()` from FixedUpdate (P2).
  Mirrors transform pose to the world if antenna animates.
- [ ] **EditMode test scaffolding.** `Tests/Editor/RH.Radar.Tests.Editor.asmdef` + first
  smoke test (`RadarEntity_Constructs_WithoutErrors`).

## Phase 1 — L0/L1 (visibility cone + range detection)

- [ ] **L0 cone scanner.** Boolean visibility through a configurable cone. Iterates
  `IThreat` registry. Outputs `RadarTrack` with quality = 1.0, no noise.
- [ ] **Threat registry helper.** `RH.Shared.ThreatRegistry` (move into RH Shared if a
  second consumer materializes). Lets the scanner enumerate all active IThreats
  without `FindObjectsOfType` (P3 testability).
- [ ] **RadarTrack struct + adapter.** Implements `ITargetTrack`. Carries TrackId,
  position, velocity (smoothed across frames), quality, lastUpdated.
- [ ] **L1 range-gated scanner.** Adds free-space radar equation: `P_r = P_t · G² · σ · λ² / ((4π)³ · R⁴)`.
  Compares SNR to detection threshold. RCS comes from `IThreat.RadarCrossSectionM2`.
- [ ] **L1 EditMode tests.** Detection range vs RCS, beam pattern cutoff, false-positive
  rate at boundary.

## Phase 2 — L2 (tracking)

- [ ] **Kalman-style track filter.** α-β filter on position + velocity. Per-track state
  carried in `state.Tracks[i]`. Promotes detection → declared track at quality threshold.
- [ ] **Track lifetime + dropout.** Tracks decay if no updates for N seconds. `Quality`
  exposes the decay so consumers can preemptively re-acquire.
- [ ] **L2 EditMode tests.** Track survives one missed frame, gets dropped after timeout,
  filtered velocity converges to ground truth within N steps.

## Phase 3 — L3 (Doppler + MTI)

- [ ] **MTI gating.** Tracks with closing velocity below `minClosingMps` get suppressed
  (ground clutter). Configurable per profile.
- [ ] **Chaff discrimination.** Chaff clouds (future IThreat impl) have falling velocity
  + high RCS — filter recognizes the signature.

## Phase 4 — L4 (TWS + ECCM)

- [ ] **Track-While-Scan.** Maintain N simultaneous tracks across multiple beam azimuths.
- [ ] **Frequency agility.** Profile defines a frequency hop pattern; jam detection per band.

## Phase 5 — Profiles + Examples

- [ ] **SPG-62 profile.** Real-world parameters (illuminator radar for ESSM SARH).
- [ ] **APG-79 profile.** F/A-18E/F AESA fire-control radar.
- [ ] **Example scene.** Drop a few aircraft, walk through scan→detect→track→lock cycle
  with HUD overlay.

## Phase 6 — Integration

- [ ] **Guided Fury seeker source from radar.** A `RadarFedTargetSource : ITargetSource`
  in Guided Fury that polls an ISensorReport. No new deps in RH Radar.

---

## Skip / fail log

(Routine writes failed-then-retried tasks here with a one-line reason.)

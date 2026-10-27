# Guided Fury — Roadmap

**This document's one job:** what to do next for the missile framework, and what is done. What exists today is in `README.md`.

## The grade: video-game level, generic (SIX, 2026-10-27)

> "Don't simplify code that we already have. Just simplify the plans and generalize them to make them safer." (SIX, 2026-10-27)

Every plan item is written to this grade and checked against the safe limits (`Assets/General Quarters/BLUEPRINT.md` §6, standing constraint 8) before it is built.
- **Missiles are archetypes, not real weapons.** A new profile is named for its role (SR-IR missile, area SAM) and carries AUTHORED game numbers. No real designators, blocks or variants, and no real-system performance tables.
- **Guidance is textbook:** pursuit, or proportional navigation with an authored gain. **A seeker is a cone, a range and a lock chance.** No seeker, datalink, fuze, warhead or autopilot internals.
- **Lethality is abstract:** a lethal radius, an AUTHORED kill chance per target class, and hit points. Nothing is derived from warhead or fragment data.
- **New archetypes fly at L0–L2.** L3 and L4 stay as built (see "Stays as built") and are not extended: no new tabulated aero, autopilot or motor detail. An archetype asked for at L3 or L4 flies there on the simple aero model.
- **Defence and EW stay generic. Opponents are generic peers.**
- **Every slice ends in a gameplay-facing definition of done:** what the player sees, how long an engagement takes to resolve, and that it resolves the same at any frame rate (P2) and on every run with the same seed (P7).

**Rules:**
- Items MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Guided Fury changes only through a PR into the protected `Guided-Fury` branch (CLAUDE.md, Branches).
- **Module lines (SIX, 2026-10-27):** Guided Fury owns all missile behaviour (seeker, guidance, motor, aero, fuze, profiles). A vehicle module carries the rails and steps Guided Fury's pure missile. Gunnery owns round flight.
- The autoroutine that used to read this roadmap was retired on 2026-10-27 (`.claude/autoroutines/KILLED.md`).

**Next up:** fix the two pre-existing test failures (Known issues), then A1, A2 and A3 (the SR-IR missile for Air Wing).

---

## Archetypes

Every number is AUTHORED for play at the range's scale, with a one-line reason in the profile's provenance. An archetype is fully described by:
- **flight:** top speed, boost and sustain thrust and time, flight time, g limit;
- **guidance:** law and navigation gain;
- **seeker:** its kind (a label: IR or radar), cone half-angle, range and lock chance;
- **damage:** lethal radius, kill chance per target class, and the hit points a non-killing hit removes.

| Archetype | Role | Seeker | Level | First user |
|---|---|---|---|---|
| **SR-IR missile** | Short-range air-to-air | IR cone, short range | L2 | Air Wing's hardpoints (W55, W58) |
| **MR-radar missile** | Medium-range air-to-air | Flies to its launch aim point, then a radar cone | L2 | Air Wing, later |
| **Point-defence SAM** | Short-range ship air defence | Exists (S1) | L2 in General Quarters | General Quarters (stays as built) |
| **Area SAM** | Long-range ship air defence | The ship guides it; a radar cone at the end | L2 | General Quarters slice B2 (G69) |
| **Anti-ship cruise missile** | Sea-skimming attack on ships | A radar cone that opens at an authored range | L1–L2 | General Quarters |
| **Land-attack cruise missile** | Waypoints to a fixed point | None, or a cone at the end | L1 | General Quarters; fictional or generic targets only |
| **SR air-to-ground missile** | Short-range attack on a marked point | A cone on the point the shooter marks | L2 | The range's ground targets |

## Stream A — Archetypes

Rewritten 2026-10-27 to game level: the six real-weapon profile items became the six new archetypes above (A3–A8), and the L5 tier is killed (K1–K5). Real-system detail removed.

- [ ] **A1. Archetype builder.** One editor command per archetype writes its profile from AUTHORED numbers, in Pat4's builder shape: values as source code, a reason per value in the provenance, and a rerun resets to the defaults. No real-system citations.
  - *Done when:* each builder writes a profile that passes `MissileProfileSO`'s validation, and a headless flyout reaches the archetype's authored top speed and flight time within 5 %.
- [ ] **A2. Lock chance on the cone seeker.** Once a target has sat inside the cone and range for the dwell time, one draw from the run's seeded source decides the lock, and the draw is logged (P7). A failed draw waits an authored retry time.
  - *Done when:* a lock chance of 0 never locks and 1 always locks after the dwell; the same seed gives the same locks; the HUD shows LOCK and NO LOCK.
- [ ] **A3. SR-IR missile** (first: Air Wing's "missiles next", W55 and W58). Built from Guided Fury's pure parts and stepped inside Air Wing's tick.
  - *Done when:* in Dogfight the player sees LOCK while a target sits inside the cone and range, fires, and the missile hits a non-manoeuvring target inside its range; an engagement resolves within the authored flight time; the same seed gives the same Fix at any frame rate.
- [ ] **A4. Area SAM** (General Quarters slice B2a, G69). At L2, like the point-defence SAM. The ship's guidance channels and illuminators are General Quarters'.
  - *Done when:* General Quarters' B2 gate is met (its `ROADMAP.md`).
- [ ] **A5. Anti-ship cruise missile.** Skims at an authored height (`seaSkimAltitudeM`, a new profile field), flies a bearing or waypoints, and opens its cone at an authored range.
  - *Done when:* on the range it holds its skim height within an authored band, finds a ship target inside its cone and hits it, the same on every run.
- [ ] **A6. Land-attack cruise missile.** Flies authored waypoints at an authored height and dives on its last waypoint from an authored point. Fictional or generic targets only (safe limits, item 3). It flies its waypoints exactly (K4).
  - *Done when:* it passes every waypoint within an authored radius and hits its end point.
- [ ] **A7. SR air-to-ground missile.** Homes on the point the shooter marks. The range's ground targets only.
  - *Done when:* from the range's launcher it hits a marked ground target inside its range.
- [ ] **A8. MR-radar missile.** Flies to the aim point it was given at launch, then locks with its cone (A2).
  - *Done when:* in Dogfight it hits a non-manoeuvring target beyond the SR-IR missile's range, and the HUD shows its time to go.

### ~~New profiles (real weapons)~~
Rewritten 2026-10-27 as A3–A8 above. Real-system detail removed; the old wording is in git at 2121da9e.

### ~~L5 (highest fidelity)~~ (killed 2026-10-27)
- [ ] ~~skipped~~ ~~L5 design doc~~ (K1)
- [ ] ~~skipped~~ ~~L5 integrator skeleton~~ (K1)
- [ ] ~~skipped~~ ~~Thrust vectoring subsystem~~ (K2)
- [ ] ~~skipped~~ ~~Staging subsystem~~ (K3)
- [ ] ~~skipped~~ ~~INS error model~~ (K4)
- [ ] ~~skipped~~ ~~Bump `HighestImplementedLod`~~ (K1: it stays at L4)
- [ ] ~~skipped~~ ~~L5 EditMode tests~~ (K1)

Real-system detail removed; the old wording is in git at 2121da9e.

## Stream B — Damage (abstract)

Rewritten 2026-10-27 to game level (safe limits, item 4): damage is a lethal radius, an AUTHORED kill chance and hit points. Real-system detail removed.

- [ ] **B1. Damage on the profile.** Each archetype carries a lethal radius, an AUTHORED kill chance per target class (light, medium, heavy), and the hit points a non-killing hit removes. When the missile detonates within its lethal radius, one seeded draw decides kill or hit, and the log line carries every term (C28).
  - The existing prop impulse (`blastRadiusM`, `blastImpulseNs`) stays: it throws range props about and is not damage.
  - *Done when:* inside the radius the draw is made and logged, and outside it nothing happens; the same seed gives the same outcome; the profiles in "Stays as built" behave as they did.
- [ ] **B2. Hits leave as RH Shared `HitReport`s.** Each vehicle module applies them to its own hit points (module lines, 2026-10-27), so an Air Wing jet or a General Quarters ship takes a missile the way it takes a round. The report counts a warhead as one round today, so the slice first decides how it carries a kill or the archetype's hit points.
  - *Done when:* a missile hit on an Air Wing jet removes its hit points through the same path as the gun's, and the kill is logged with its shooter.
- [ ] **B3. HUD and stats.** `LodComparisonStats` shows KILL, HIT or MISS for every missile, with its draw.
  - *Done when:* for every shot on the range the player can read what happened and why.
- [ ] ~~skipped~~ ~~Warhead profile authoring~~: rewritten as B1.
- [ ] ~~skipped~~ ~~Blast model~~ (K6).
- [ ] ~~skipped~~ ~~Fragmentation model~~ (K7).
- [ ] ~~skipped~~ ~~Damage classes from published tables~~: rewritten as B1's AUTHORED classes.
- [ ] ~~skipped~~ ~~`IDamageable` upgrade~~: rewritten as B2. The hit-report contract now lives in RH Shared.
- [ ] ~~skipped~~ ~~HUD / stats integration~~: rewritten as B3.
- [ ] ~~skipped~~ ~~Damage EditMode tests~~: rewritten as B1's definition of done.

Real-system detail removed; the old wording is in git at 2121da9e.

## Stream C — Test Coverage + Scenario Library

- [ ] **Test-coverage audit.** Count test classes per Core/ subfolder. Identify gaps.
  Write report to `_audit/tests-<date>.md`.
- [ ] **Property tests** for `StableLookRotation` — fuzz random forwards, assert quaternion
  is valid + forward axis matches input.
- [ ] **Scenario library.** Convert `EngagementScenarios` enum to SO-driven scenario
  presets (`ScenarioSO`). Each scenario = world setup + expected outcome.
- [ ] **Crossing / head-on / tail-chase / pop-up scenarios** as SOs.
- [ ] **Salvo coordination scenarios.** N missiles vs M targets, time-overlap, weapon
  pairing optimality.
- [ ] **Replay support.** Record `MissileState` per step at L1+ to a binary log. Editor
  tool to scrub/replay. Foundation for regression tests (snapshot-compare).
- [ ] **Snapshot regression harness.** Run a canonical scenario, compare summary
  statistics (impact time, miss distance) to a golden value. CI gate.
- [ ] **PlayMode smoke scenes.** One scene per fidelity tier (L0..L4; L5 killed, K1). Routine adds
  these to the verification gate.

---

## Known issues / tracked follow-ups (2026-09-08 session)

Filed during the terminal-guidance investigation; each is real, none is urgent enough
to block the current streams. Pick up opportunistically or when a stream touches the area.
Items with a fix path were rewritten to game level on 2026-10-27 (real-system detail removed).

- [x] **~~Vertical-launch pitch-over~~ DONE (2026-09-08).** `BlendedPursuitProNav`
  (closing-ratio-weighted pursuit->ProNav) + `seekerMidcourseDatalink`. VLS closes to 20 m
  (was 3.1 km ballistic). Air battery unregressed. New law is selectable per profile.
- [ ] **Surface attack is the ground-attack archetypes' job, not a SAM's.** Below-horizon
  ground targets stay out of the point-defence SAM's envelope; the regression test pins its
  ground misses at <= 30 m. Surface attack belongs to A6 and A7, which dive on their target
  from an authored point (a game rule; K9).
- [ ] **Crossing-target near-miss band.** Pure PN + body-fixed 30-degree cone closes
  crossing shots to 12-16 m (fuze 10 m). Blast (25 m) covers it. At game level the archetype's
  authored lethal radius decides these shots (B1); no deeper guidance or seeker model (K8).
- [ ] **Two pre-existing EditMode test failures (predate 2026-09-08, verified via stash
  bisect):** `L3_AoaProducesLift_PitchedBodyPullsToward` (lift pulls velocity DOWN — sign
  or frame bug in the L3 lift path) and `EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses`
  (closes 1213 m vs required 802 m). Both look like Phase-6 collateral in L3/L2 paths.
  Fix these before A3 (SIX, 2026-10-27). The fix restores what the tests already pin; it does not deepen L3.
- [ ] **Proximity fuze fires on ANY collider** (`CheckProximityFuze` has no target/ground
  filtering — the "detonated 150 m past" symptom was the fuze catching the ground plane).
  Game-level fix: only targets set it off (a layer filter), and the ground does not.
- [ ] **Two fullscreen cameras stacked in MissileRange** (TurretCab chase cam depth 0
  renders over Main Camera depth -1; both render full HDRP frames every frame). Decide:
  PIP inset for the turret cam, or disable Main Camera render when chase is active.
- [ ] **Two active AudioListeners** in MissileRange (Main Camera + TurretCab camera).
- [ ] **Strip `BurkeMissile` + embedded "Missile Cam" from the point-defence SAM's shell
  prefab (S2).** NeutralizeForeignComponents disables them per-launch (with a warning each
  shot); removing them from the prefab silences the log and saves the per-launch scan.
  A legacy-component cleanup; it does not change the missile.
- [ ] **GuidedFury_TestRunner auto-launches on play** (`autoLaunchOnStart = true` in the
  scene) — fires a missile at t+0.5 s every time play starts. Fine for a test range,
  surprising for demos; consider defaulting off now that the control panel exists.
- [ ] **`scripting_class_is_subclass_of called with NULL` assert** on every editor
  session (would crash a build per Unity's own message). Bisect which package/asset
  triggers it after the 6000.4 upgrade.
- [ ] **Blast impulse vs light props.** 20 000 N-s throws 100 kg debris at ~120 m/s
  (cinematic but cartoonish). If it bothers, add a per-body mass-cap or scale impulse
  by target mass class on the profile.
- [ ] **HDRP top-down/orthographic hand-rendered captures come out black.** A manually
  created Camera.Render() to a RenderTexture does not pick up HDRP exposure/volume, so
  overhead range renders are unusable. Use in-game play-mode cameras for screenshots, or
  set up a proper HDRP camera + exposure override for a map view.
- [ ] **Range polish backlog:** ship-hulk targets need a HittableBox sizing pass for the
  fuze; naval water is a flat unlit plane (no shader); mesa-gantry A2G geometry not yet
  play-verified end-to-end (pure-sim only); airborne-testbed wing-pylon muzzle assumes
  level flight (fine on the racetrack straights, off in the turns).
- [ ] **Untracked vendor packs** (DAVFX, RH Aviator, RH Radar, RH Shared, _VFX, DeepWater
  Input scripts) sit uncommitted in the working tree — decide commit vs .gitignore.

---

## Stays as built (not extended)

Existing code, assets and tests that carry real names, or more fidelity than the game-level grade. **They stay exactly as they are:** nothing here is changed, simplified or deleted, they keep working and they keep their tests. **The rule is that future work does not deepen them:** no second real-system profile on their pattern, no new tabulated aero, autopilot or motor detail, and no new plan item that takes them as its template. New plans call them by archetype.

| # | What | Where | Note |
|---|---|---|---|
| S1 | The point-defence SAM profile and its builder: a real SAM authored at L4 from public data, with Mach-dependent aero tables (`README.md`, "First authored missile") | `Examples/Profiles/RIM-162_ESSM.asset`, `Examples/Editor/EssmProfileBuilder.cs` and its menu command | General Quarters flies it at L2. It is the one real-system profile; A1's builders follow Pat4's shape with AUTHORED archetype numbers instead |
| S2 | The point-defence SAM's model prefab, with its legacy components neutralised at launch (AP8) | `Assets/rim-162essm/ESSM Shell.prefab` | The Known issues cleanup removes legacy components only |
| S3 | The L4 stack: full-aero 6DOF integrator, tabulated aero, two-loop fin autopilot, boost-sustain motor, and the aero-curve fields on the profile | `Core/Integrators/FullAero6DofL4Integrator.cs`, `Core/Aero/TabulatedAeroModel.cs` (`AeroModelKind.Tabulated`), `Core/Autopilot/SurfaceDeflectionAutopilot.cs`, `Core/Propulsion/BoostSustainThrustModel.cs`, `ScriptableObjects/Profiles/MissileProfileSO.cs` | New archetypes may fly at L3 or L4 on the simple aero model; no new tables or autopilot detail are authored |
| S4 | The unbuilt L5 value of the level enum, and its comment | `Core/Integrators/MissileLod.cs` (`MissileLod.L5_HardwareInTheLoop`) | Removing an enum value is breaking (Pat3). It is never built (K1); a request for it falls back to L4 and says so |
| S5 | Tests that name the point-defence SAM or pin L4 | `Tests/Editor/TerminalGuidanceRegressionTests.cs` (the `Essm_*` tests), `Tests/Editor/L4FlightStabilityTests.cs`, `Tests/Editor/L4IntegratorTests.cs` | They pin existing behaviour and keep running |
| S6 | Code comments and tooltips that use real missiles as examples | `Core/Propulsion/BoostSustainThrustModel.cs`, `Core/Propulsion/IThrustModel.cs`, `Core/Missile/MissileBehaviour.cs`, `ScriptableObjects/Profiles/MissileProfileSO.cs`, `Examples/Editor/MissileRangeSceneBuilder.cs`, `Examples/Editor/WeaponsRangeSectorBuilder.cs`, `Examples/Scripts/MissileCameraController.cs` | No code is edited by a plan change |
| S7 | General Quarters' missile pieces: the weapon-class values, the area SAM's TEWA band, the loadout, the level bridge and the trail effect | `Assets/General Quarters/Core/CombatTypes.cs` (`WeaponClass.Sm2`, `Essm`, `Harpoon`, `Tomahawk`), `Editor/Ddg51CombatData.cs`, `ScriptableObjects/DDG51_GQ_Loadout.asset`, `Core/Magazine/GuidedFuryBridge.cs`, `Prefabs/Effects/GQ_FX_Trail_Essm.prefab` | `WeaponClass` values go into the Fix, so they never change. New plans call them by archetype (G69) |
| S8 | Legacy missile code outside Guided Fury | `Assets/Scripts/Missile Scripts/`, `Assets/_Archive/` | Not Guided Fury's; untouched |

## Killed

| # | Item | Killed | Reason |
|---|---|---|---|
| K1 | The L5 tier: design doc, integrator, the `HighestImplementedLod` bump, tests | 2026-10-27 | A hardware-in-the-loop tier is engineering grade by definition and serves no game need (safe limits, item 2, and the grade above). Guided Fury tops out at the L4 already built (S3); the enum value stays unbuilt (S4) |
| K2 | Thrust vectoring | 2026-10-27 | An L5 delta. In play, agility is the archetype's authored g limit |
| K3 | Staging | 2026-10-27 | An L5 delta. A booster falling away is presentation; flight uses boost and sustain with authored numbers |
| K4 | A navigation-error model for weapons | 2026-10-27 | Real-system guidance detail. A game cruise missile flies its authored waypoints (A6) |
| K5 | Signal-aware and gimbaled seekers (`README.md`, "deliberately not here") | 2026-10-27 | A seeker is a cone, a range and a lock chance (A2); its kind is a label with its own authored numbers. Track-memory coast already exists |
| K6 | A blast model | 2026-10-27 | Engineering-grade lethality (safe limits, item 4). Damage is B1's lethal radius and kill chance |
| K7 | A fragmentation model | 2026-10-27 | Engineering-grade lethality (safe limits, item 4) |
| K8 | Deeper guidance or seeker modelling to close crossing near-misses | 2026-10-27 | The archetype's authored lethal radius decides those shots (B1) |
| K9 | A dedicated surface-attack guidance law | 2026-10-27 | The ground-attack archetypes dive from an authored point (A6, A7) |

The rule these kills produced: **plans state fidelity as an archetype and an authored number, not a real system** (METHODOLOGY C47).

## Change log
- **2026-10-27** — Rewritten to video-game grade and made generic, at SIX's direction ("Just simplify the plans and generalize them to make them safer"). Stream A's six real-weapon profiles became six archetypes (A3–A8) after a builder (A1) and a seeker lock chance (A2); Stream B became abstract damage (B1–B3); the L5 tier and the engineering-grade items were killed (K1–K9); "Stays as built" lists the existing code that is not extended. Every item was checked against the safe limits, General Quarters `BLUEPRINT.md` §6 standing constraint 8. Real-system detail removed; the previous wording is in git at 2121da9e. No code, asset, test or scene changed. Engineering-grade original preserved outside the repo (from 2121da9e).

## Skip / fail log

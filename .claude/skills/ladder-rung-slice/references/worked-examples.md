# Worked examples

The three rungs built so far, in build order, then Air Wing's version of the same contract. Paths are under `Assets/General Quarters/` unless they say otherwise. Each section gives the decisions, the commits, what landed where, the tests, what verification said, and what the slice taught.

## Guns L1 (Stream B slice B1, 2026-10-24)

- **Decisions:**
  - G34: guns first.
  - G35: a 3-DOF point mass.
  - G36: a shot group, with one seeded draw per burst.
  - G37: slew, and ship motion as aim error.
  - G38: a ballistic lead.
  - G39: the Mk 45's two fuzes.
  - G40: CIWS closed-loop spotting.
  - G41: the G7 curve times one CALIBRATED form factor per projectile.
- **Commits:**
  - `753dc1e`, the slice: 29 files, with the ballistics, `GunMountL1`, the data, the tests and the docs;
  - `d02d226`: the presentation mounts follow the core's lay (`IGunMount.TryGetLay`), so what you see is what fired;
  - `76c2d6a`: the Mk 45 jitter SIX saw on the range (AP21, trap 3).
- **Core:**
  - `Core/Magazine/GunsL1.cs` holds `GunMountL1`.
  - `Core/Magazine/Guns.cs` holds `GunMountBase`, the target choice L0 and L1 share, so L0's behaviour and golden stayed put, and `GunFactory`.
  - The ballistics (`Atmosphere`, `DragCurve`, `FiringTable`, `ShotGroup`) moved to `Assets/Gunnery/Core/Ballistics/` on 2026-10-27 (Gunnery K1), with their own banned-API scan.
- **Data:**
  - `GunMountProfileData.Ballistics`, written by `Editor/Ddg51CombatData.cs` (`Mk45L1`, `CiwsL1`, `Mk38L1`, from :304);
  - every number labelled in `Calibration~/GUNS-SOURCES.md`;
  - form factors CALIBRATED to published ranges; dispersions, latencies and kill chances AUTHORED.
- **Adapter:** `ShipCombat.ReadOwn` (`Modules/Combat/ShipCombat.cs:205`) reads roll, pitch and their rates from PhysX into `OwnShipState`.
- **Tests:** `Tests/Editor/GunsL1Tests.cs`.
  - The atmosphere and G7 at spot values; vacuum v²/g.
  - The calibrated ranges, with the published figures as VALIDATION.
  - Fire control and lead; slew before the first shot; ship motion as aim error; CIWS spotting; both fuzes.
  - `L1_WithoutBallisticsData_RefusesToBuild`.
  - `Saturation_AtGunsL1_Resolves_Repeats_AndStaysInBudget`.
  - From `76c2d6a`: `Mk45_StaysOnItsTarget_UntilItIsGone`.
- **Verification:**
  - 134/134 (General Quarters 85, Sea State 49).
  - Guns alone against 8 sea-skimmers: L0 killed 0, L1 killed 6.
  - The core: 0.060 ms per fixed step (L0 0.066).
  - Play mode on the range: the Mk 45 laid and fired at 12,965 m, and PhysX roll gave 22.8 m of aim error.
- **Not verified, and said so:** every lethality figure (AUTHORED); the Phalanx drag shape (form factor 3.25).
- **Taught:**
  - C26–C29 (METHODOLOGY changelog, 2026-10-24), then AP21.
  - The L1 Fix moved in `76c2d6a`, because behaviour changed on purpose: the Mk 45 kept firing. There is no golden at L1, only the run-twice pin.

## Damage Control L1: compartments and flooding (Stream C slice C1, built 2026-10-24, gate closed 2026-10-25)

- **Decisions:**
  - G53: compartments from Blender's stations, with decks and side splits as data.
  - G54: decks and wing spaces.
  - G55: the water's mass and centre of mass ride on the Rigidbody. SIX's pick differed from the leaning.
  - G56: orifice flow.
  - G57: Breach Orders and leaker hits; the hole is CALIBRATED to one public figure, flagged.
  - G58: physics only.
  - G59: the board and the timeline.
  - G60: loss counted from the flooding.
- **Commits:**
  - `84fb04b`, the slice: 50 files;
  - `10902d3`: METHODOLOGY AP22, AP23, C30 and C31, and three `unity-mcp-workflow` traps;
  - `a289614`: the ROADMAP and HANDOFF re-dated to when the PhysX gate closed.
- **The gate was set before the first measurement** (the ROADMAP item):
  - list within 5 % (or 0.2°); trim within 15 % (or 3 cm); sinkage within 15 % (or 2 cm); GM within 5 %;
  - past the margin she sinks, and repeats exactly.
- **Core:** `Core/DamageControl/`.
  - `DamageControlL1.cs`.
  - `FloodPlan.cs`: the spaces, cut from the hull's own buoyancy columns (C30).
  - `FloodModel.cs`: the water.
  - `FloodHydrostatics.cs`: the damaged GM.
  - `FloodMass.cs`.
- **Adapter:** `Modules/Hull/ShipFlooding.cs`, at `[DefaultExecutionOrder(-110)]`, before buoyancy.
  - It puts the water on the Rigidbody through `ShipHull.ApplyFloodWater` (`Modules/Hull/ShipHull.cs:47`).
  - It samples the sea at the openings.
  - It stops a lost ship's physics once she is under (AP23).
- **Data:**
  - `ShipLoadoutSO.flooding` (`FloodingTuning`) and the hull profile (`ShipHullProfileSO`);
  - permeability from SOLAS II-1 Reg. 7-3;
  - the hole size CALIBRATED to one public figure;
  - the deck height, wing breadth, leak paths and capsize heel AUTHORED.
- **Prediction tool:** `.claude/skills/hull-stability-check/scripts/flood_predict.py`, a closed form on the same columns.
- **Tests:** `Tests/Editor/DamageControlL1Tests.cs`, with `Tests/Editor/FloodedHullRig.cs` (Sea State's probe jobs carrying the water).
  - `SpaceOpenToTheSea_HoldsExactlyTheBuoyancyItsColumnsLose` and `Orifice_FillsAsTorricelliSays`.
  - `FloodedWing_ListsTrimsAndLosesGm_AsPredicted`, `FloodedBowHold_TrimsAndSinks_AsPredicted` and `PartFilledHold_FreeSurfaceComesFromTheLevel`.
  - `PastTheMargin_SevenStations_SinksHer_TheSameWayEveryTime`.
  - `DamageControlL0_DoesNotFlood`.
- **Verification:**
  - 149/149 (General Quarters 100, Sea State 49; was 136).
  - Wing 06 S: a list of 4.321° against 4.34° predicted, and 4.337° in PhysX.
  - Sinking: the same tick and Fix on two runs.
  - The core: 0.33 ms per fixed step at the worst.
  - The golden Fix `2AF5C040E4AC6B5E` is unchanged at Damage Control L0.
- **Safe limits**, written into the ROADMAP item: the physics is textbook and the arrangement AUTHORED, so the margin found is the game hull's, and it says nothing about the real ship's survivability.
- **Taught:** AP22, AP23, C30, C31.

## Radar L1 + Tracks L1 (Stream A, 2026-10-27)

- **Decisions:** prepared overnight as `RADAR-L1-QUESTIONS.md` (Q6: R1–R8, each with its leaning). SIX took every leaning:
  - G61: split the suite's pure core.
  - G62: SPY-1's role only.
  - G63: generic and AUTHORED, with one public range figure.
  - G64: tracks in the same slice.
  - G65: one dBsm per kind.
  - G66: a Swerling 1 draw per look, from the run's seed.
  - G67: constant-γ clutter from Sea State's degree.
  - G68: identity from truth.

  G70 was added as Proposed while building, with Q7 for SIX.
- **Commits:**
  - `5bdb3493`: the shared physics becomes `RHRadarSuite.Core`, plus the textbook relations General Quarters needed, with 4 new suite tests (46/46). Its asmdef, `Assets/RH Radar Suite/Core/Detection/RHRadarSuite.Core.asmdef`, sets `noEngineReferences`, and `Core/Tracking` joins it through an `.asmref`.
  - `831e2ff6`: Radar L1 and Tracks L1 in the CIC, the loadout at L1, and the tests pinned back at L0.
  - `1439145d`: the ROADMAP, the HANDOFF, the BLUEPRINT (G70, Q7, the §4 rows) and METHODOLOGY C48.
  - `a7afe8a8`: the play-mode check on the range.
  - `d93e245f`: restores the 40 Stream A paths that `2afbb624` had reverted from a stale index (trap 1).
- **Core:**
  - `Core/Cic/Radar.cs` holds `RadarL1` and `SensorFactory`.
  - `Core/Cic/Tracks.cs` holds `TrackManagerL1`.
  - `Core/Simulation/SeededRandom.cs`.
- **Adapter:** `ShipCombat.ReadSea` feeds Sea State's conditions to `CombatSystem.Sea`. Without a Sea State director, the core keeps a calm sea and logs that once.
- **Data:**
  - `RadarL1Data` and `TrackL1Data`, written by `Ddg51CombatData.RadarL1()` and `TracksL1()`, with `RadarL1Provenance`.
  - The peak power is solved from one public range figure (CALIBRATED). Its pairing with 0 dBsm and Pd 0.5 is AUTHORED and flagged.
- **Tests:** `Tests/Editor/RadarL1Tests.cs` (13), with `TestLoadouts.Ddg51L1`.
  - `Calibration_PutsTheReferenceTargetAtItsPd_AtThePublicRange`.
  - `SeededLooks_DetectAsSwerling1Predicts_AndSpendTheRunsDraws`.
  - `L1_WithoutItsProfileData_RefusesToBuild` and `SeededRandom_DrawsOnlyFromTheRunsGenerator`.
  - `RaidL1_SameSeedSameFix_EveryTick_AndEveryKillTracesToADetection`.
  - `Saturation_OnTheRangeLoadout_InTheRangesSea_Repeats_IsMeasured_AndStaysInBudget`.
  - Pinned back at Radar and Tracks L0: the golden, `GunsL1Tests.Mk45_StaysOnItsTarget_UntilItIsGone`, and the AI captain's C3 and T1 rule tests.
  - `CoreContractTests`' fallback expectation moved to L1.
- **Verification:** headless, in the background mirror.
  - `RadarL1Tests`: 13 tests.
  - General Quarters: 113 tests, 111 passing in the mirror's run. `Prefab_IsAssembledFromTheBlenderModel` fails in the mirror before and after the change. The sinking test timed out cold and passed warm.
  - Sea State 49/49; RH Radar Suite 46/46.
  - Play mode (`a7afe8a8`): 10 tracks made and none dropped; 8 of 8 sea-skimmers killed; track error mostly 20–60 m.
- **Taught:** C48. Run a locked model on the game's own scenario before wiring it in. If a missing term flips the result, add it as a seam whose zero is the decision.

## Air Wing: the same contract

- **The contract:** `Assets/Air Wing/BLUEPRINT.md` §4 ("C14, as in General Quarters"). One state struct, `AirframeState`, carries across every level.
- **The ladder:** a `PartLadder` (`Assets/Air Wing/Core/Fidelity/FidelityLadder.cs:54`). `FidelityResolution.Reason` says why when the reason is not "not built yet". An example is a pairing rule: Airframe L1 and Flight Controls L1 build only together (ROADMAP slice 2b).
- **Its tests:** in `Assets/Air Wing/Tests/Editor/`.
  - Fallback: `CoreContractTests.Ladder_RequestAboveBuilt_FallsBackOutLoud`.
  - Fix pins: `FlightL0Tests.TwoRunsWithTheSameInputs_GiveTheSameFixOnEveryTick`, and `ControlsL1Tests.AtL1_TheFlightCore_IsBitIdentical_AndReplays`, which also replays the run from its input log.
  - Banned APIs: `CoreBannedApiTests.FlightCore_UsesNoBannedApis`.
- **What differs from General Quarters:**
  - an aircraft changes only through Pilot Inputs from a seat (W6), not through Orders;
  - the fingerprint is `FlightFix`;
  - the budget is per aircraft (W21).
- **Its L1 rungs** are slices 2a (Airframe L1) and 2b (Flight Controls L1) in `Assets/Air Wing/ROADMAP.md`. Slice 2b fixed its criteria before tuning, as slice C1 set its gate before measuring.

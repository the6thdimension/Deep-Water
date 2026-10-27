---
name: ladder-rung-slice
description: "Build one rung of an element's fidelity ladder in Deep Water as a gated slice: L0 is the simplest whole version, and each level up adds physics and comes with tests. Covers orienting on the module's HANDOFF, ROADMAP item and BLUEPRINT decision rows (build only from Locked rows; record a Proposed row and ask SIX for anything uncovered), the safe-limits check against BLUEPRINT §6 #8, labelled ScriptableObject data, an engine-free pure core with a banned-API guard, an adapter and a FixedUpdate tick, the tests a rung needs (behaviour, refusal and an out-loud fallback, and a Fix pin: the same seed and Orders give the same Fix), the verification ladder, close-out of every document and memory, and committing and levelling the branches without the stale-index trap. Use it whenever SIX says 'next slice', 'L1', 'L2', 'the next rung', 'fidelity ladder', 'Stream A', 'Stream B', 'Stream C', 'deepen the radar / guns / damage control / engine room / helm / TEWA', or 'close out the slice', in General Quarters, Air Wing or any module with the same ladder contract. Proven three times: Guns L1, Damage Control L1 flooding, Radar L1 + Tracks L1. Not for missile rungs."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Ladder-rung slice

**The contract** (General Quarters `BLUEPRINT.md` §4; Air Wing's §4 is the same, METHODOLOGY C14):
- each element is an interface plus one implementation per level, built by a factory from `(level, profile)`;
- a request for a level that isn't built runs at the highest built level **and says so**;
- each built level ships with EditMode tests, and the element's tests run at every level it has.

The one table of built levels is `FidelityLadder.HighestImplemented` (`Assets/General Quarters/Core/Fidelity/FidelityLadder.cs`). Air Wing registers levels on a `PartLadder` instead.

**Proven three times in General Quarters.** Read [references/worked-examples.md](references/worked-examples.md) first; the slice closest to yours is the template.

| Slice | Decisions | Commits |
|---|---|---|
| Guns L1 (Stream B, B1) | G34–G41 | `753dc1e`, `d02d226`, `76c2d6a` |
| Damage Control L1, flooding (Stream C, C1) | G53–G60 | `84fb04b`, `10902d3`, `a289614` |
| Radar L1 + Tracks L1 (Stream A) | G61–G68, G70 Proposed | `5bdb3493`, `831e2ff6`, `1439145d`, `a7afe8a8`, `d93e245f` |

**Not for missile rungs** (Guided Fury, Stream B2): SIX gave those to Codex. Where a rung meets a missile, treat the missile as a black box behind its seam.

Each step ends in a gate; don't start the next until it passes. `templates/slice-checklist.md` is the copy-able list.

## 1. Orient

1. **Read, in order:** the module's `HANDOFF.md` (hard-won findings, next steps); the slice's `ROADMAP.md` item; the BLUEPRINT §4 row for the element and every decision row the item cites; the METHODOLOGY entries those rows name. Search MemPalace for the element. Open the scene that will run it (CLAUDE.md: the Inspector wiring matters more than the code). Look for prior art: Radar L1 reused RH Radar Suite's core (G61).
2. **Build only from Locked rows, and never re-litigate them** (HANDOFF §7).
3. **A choice no row covers** gets a **Proposed** row: "PROPOSED by Claude, not decided" with the reason and what was measured, the alternative in the Leaning column, and a priced question in §9 put to SIX as multiple choice. If the rung can run without the answer, build it behind a seam whose zero reproduces the Locked row exactly, and measure both ways (C48; G70, Q7). Row formats: [references/doc-formats.md](references/doc-formats.md).
4. **Five or more open questions?** Write them up first, each with a leaning, as `RADAR-L1-QUESTIONS.md` was (the `concept-doc-interview` skill). The answers become rows.
5. **Confirm the plan with SIX** in one paragraph: intent, files, trade-offs (CLAUDE.md). Skip only if SIX already said go.

**Gate:** every slice item maps to a Locked row; anything uncovered is a Proposed row with its question.

## 2. Safe limits

Check every item against BLUEPRINT §6 #8 before building (Air Wing's §6 #8 applies it in full).
- **Stays fine:** public, cited specifications; textbook physics; abstract lethality (hit points, AUTHORED kill chances); generic systems and scenarios.
- **Not done:** non-public data, code for real hardware, real-world targeting, engineering-grade lethality, defeat-optimisation against a real system, CBRN, specific real peers.
- **Shape the rung** as a generic system in a role ("a generic S-band array in SPY-1's role", G63), with AUTHORED numbers and at most one public figure per calibrated mechanic (G57, G63).
- **Write the check down:** a "Safe limits, checked before the build" bullet in the ROADMAP item (as slice C1 has), and "Checked against standing constraint 8" in the BLUEPRINT change log.
- **A request that would cross a line** is said so plainly and reshaped toward the game side, never built quietly.

**Gate:** the check is written in the ROADMAP item.

## 3. Data

- **Every number lives in a ScriptableObject** (P1). The level's data is a `[Serializable]` struct beside its core (`RadarL1Data`, `Core/Cic/Radar.cs:31`). The loadout asset carries it (`ShipLoadoutSO`), an editor builder writes it with a provenance paragraph (`Editor/Ddg51CombatData.cs`: `RadarL1()` at :193, `RadarL1Provenance` at :210), and `ShipLoadoutSO.Bake()` hands the core a plain struct.
- **Label every number** (ship-builder's vocabulary), in the provenance and in any sources file:

| Label | Means |
|---|---|
| VERIFIED | Read on the page of a cited source |
| MEASURED | Measured from the mesh or the hull's columns |
| DERIVED | Computed from VERIFIED or MEASURED values |
| CALIBRATED | Fitted to one named public figure, which it cites. One number per figure (C16) |
| VALIDATION | Compared with a figure, not fitted to it; report the gap honestly |
| AUTHORED | A choice with a reason, because nothing public exists. Say so |

- **Many numbers get a sources file** in `Calibration~/`: value, label and source per item (`Calibration~/GUNS-SOURCES.md`).
- **Empty beats fake.** A built level with empty data throws `ArgumentException` (`IsSet`; `Radar.cs:186`, `Guns.cs:261`). It never falls back silently.

**Gate:** every field has a label, and the provenance names each source.

## 4. Pure core, adapter, tick

- **Core:** plain C#, one class per level behind the element's interface (`RadarL0`/`RadarL1 : ISensorModel`, `GunMountL0`/`GunMountL1 : IGunMount`). The factory switches on `FidelityLadder.Resolve` (`SensorFactory.CreateRadar`, `GunFactory.Create`).
  - Draws come only from the run's `DeterministicRng`, each logged as a chance root. A library that wants a `System.Random` gets `SeededRandom` (`Core/Simulation/SeededRandom.cs`).
  - Outcome state goes into the Fix (`CombatSystem.Fingerprint`). Presentation data never does (C22).
  - Raise `HighestImplemented` in the same commit as the level and its tests (the table's own comment says so).
- **Engine-free guard:** files under `Core/` are already scanned by `CoreBannedApiTests.CombatCore_UsesNoBannedApis`. A core elsewhere needs its own: a copy of the scan (`Assets/Gunnery/Tests/Editor/GunneryCoreBannedApiTests.cs`), or an asmdef with `"noEngineReferences": true`, so the compiler refuses UnityEngine (`RHRadarSuite.Core`, `5bdb3493`).
- **Adapter:** a MonoBehaviour reads the world into the truth feed and steps the core from `FixedUpdate` (`Modules/Combat/ShipCombat.cs:152`, `[DefaultExecutionOrder(-100)]`). Order matters when it feeds another component: `ShipFlooding` runs at −110, before buoyancy. A missing world input is logged once and falls to a stated default (`ShipCombat.ReadSea`, :230).
- **Lower rungs behave as before** (`DamageControlL1Tests.DamageControlL0_DoesNotFlood`).

**Gate:** it compiles (`unity-mcp-workflow`'s `wait_build.sh` on a new class or method name), and the banned-API test is green.

## 5. Tests at the rung

One file per rung, `Tests/Editor/<Element>L1Tests.cs`, all EditMode and headless. The real patterns, with paths and lines: [references/test-patterns.md](references/test-patterns.md).

1. **Behaviour against an answer fixed before the first run:** a closed form, a textbook identity or a Python mirror (vacuum v²/g, Torricelli, `flood_predict.py`, Swerling 1). Write the tolerances in the class doc first (`DamageControlL1Tests`, lines 23–25). When a gate fails, find which side is wrong; a prediction that left out physics is fixed, and the tolerance kept (AP22).
2. **Refusal and fallback:** the rung without its data throws (`RadarL1Tests.L1_WithoutItsProfileData_RefusesToBuild`); a level above the highest built falls back and says so in the log (`CoreContractTests.Ladder_FallsBackToTheHighestBuiltLevel_AndSaysSo`, whose expected level moves when you raise the table, as in `831e2ff6`).
3. **The Fix pin** (`templates/fix-pin-test.cs`): the same seed and Orders, run twice, give the same Fix, on every tick where that's cheap; another seed gives another; every kill or verdict has a cause chain ending in roots; the core stays inside 1 ms per fixed step (G16). The new rung gets **no golden constant** (C29).
4. **The golden keeps its levels.** When the shipped loadout moves up a rung, pin the old levels in the golden test (`PresentationTests.Saturation_Fix_IsUnchangedByPresentationData` sets Guns, Damage Control, Radar and Tracks to L0), and in any test whose meaning depends on the old level.

Measurements go out as tagged lines (`Debug.Log("[GQ] …")`); the ROADMAP's results are copied from them.

**Gate:** all green, with the test count before and after.

## 6. Verification ladder

Cheap to expensive; stop when confident.
1. **EditMode:** every test assembly the slice touches (`DeepWater.GeneralQuarters.Tests.Editor`, `DeepWater.Sea.Tests.Editor`, `RHRadarSuite.Tests.Editor`, `DeepWater.Gunnery.Tests.Editor`), through `unity-mcp-workflow`'s `run-tests.cs`. Pin the counts: "149/149 (General Quarters 100, Sea State 49; was 136)". Name any test that fails before and after (`831e2ff6`).
2. **Budget:** the core's ms per fixed step at the rung's worst case. A speed claim needs `unity-perf-ab`.
3. **Play mode in the scene:** Deep Water → General Quarters → Play the Range (`Examples/Scenes/GQ_Range.unity`), recorded per fixed step with `play-recorder.cs`. SIX may be driving: read the order log's issuers before trusting a recording, and ask before stopping play.
4. **Label every claim:** **verified** (seen running: tests green, or the play-mode run), **build-verified** (compiles, not run), **unverified** (not run; say what is missing).

**Gate:** the commit body has a Verification list with counts and a "Not verified" list.

## 7. Close out

- **ROADMAP:** tick the item `[x]` with the date its gate closed (`a289614`), the commits and the counts; add Results, Found, Safe limits and "Not at L1"; a change-log line; anything that failed on the way goes in the skip / fail log.
- **HANDOFF:** the "Last verified" counts, a hard-won finding, a verdict row (CHAMPION, ITERATE, FAILED, SHELVED or PROMISING-NOT-PROVEN), "What is where" rows, next steps, glossary terms.
- **BLUEPRINT:** the §4 row notes what the rung built; new rows (Proposed unless SIX picked); a change-log line with the safe-limits check.
- **METHODOLOGY** through `methodology-scribe`: one entry per lesson, citing the commit.
- **Memory:** the module's auto-memory note and its `MEMORY.md` line; verbatim findings to MemPalace.

**Gate:** every document is committed, in the `feat` commit (`753dc1e`, `84fb04b`) or a `docs(<module>):` commit right after it (`1439145d`). Formats: [references/doc-formats.md](references/doc-formats.md).

## 8. Commit and level

1. **Commit with `commit-to-branch`** and `--deny 'UnityMcpUnlock|/DAVFX/'`, one commit per concern. Body: Problem, Change (with decision ids), Verification (measured counts), Not verified.
2. **Level with its `level_branches.sh`:** `--protected Guided-Fury --repo the6thdimension/Deep-Water`, then every working branch. `Guided-Fury` changes only through the PR the script opens. Never list `archive/guided-fury-wip-20260908`.
3. **After levelling,** check every other worktree whose branch moved (`git worktree list`; commit-to-branch's rule). Report the hashes and the PR link.

## Traps (each cost real time once)

1. **A staged index in the shared checkout commits a stale tree** (2026-10-27: `2afbb624`, repaired by `d93e245f`). Levelling moves the checked-out branch's ref, but `level_branches.sh` resets that checkout's index only when it runs there with nothing staged, and never from a worktree. The stale index holds the old tree, and the next `git commit` writes it back: `2afbb624` put 40 Stream A files back to before Stream A.
   - Never stage there while another session might level. Use the scratch index (`commit-to-branch`) or a pathspec commit (`git commit -F <msg> -- <paths>`).
   - Just before committing: `git fetch origin`, check that HEAD is the branch's fresh tip, and that `git diff --cached --name-only HEAD` lists only your paths. Repair with a restore commit, never a rewrite.
2. **A rung that spends the run's draws moves every later chance draw.** On the range loadout, Radar L1 would have moved the golden Fix and broken three tests written on L0's perfect tracks; `831e2ff6` pins all four at Radar and Tracks L0 (C29). Each slice added its line to the golden: guns L0 in `753dc1e`, Damage Control L0 in `84fb04b`.
3. **EditMode missed a flicker that play mode showed** (`76c2d6a`, AP21). A firing table that lost its bracket near its floor said "out of reach" on alternate ticks, and the Mk 45 dropped and retook its target. On a real raid, log the rung's decision every tick and count drops, changes and reversals.
4. **A closed-form gate that leaves out a term the new case switches on** (`84fb04b`, AP22): twice the prediction was wrong and the model right. Fix the prediction, not the tolerance.
5. **A body the model declares lost, left running in PhysX,** fell without limit and crashed the Editor (AP23). Stop it once the verdict is in.
6. **A Locked model outside its regime** (`831e2ff6`, C48): constant-γ clutter at sub-degree grazing lost 20 of 24 threats. Add the missing term as a seam whose 0 is the decision, propose a row (G70), put both numbers to SIX. Don't bend the Locked row.
7. **The background mirror's first run after a code change is cold:** hull and flood tests ran 7–19× slower, and the sinking test hit its timeout. Time heavy tests warm. MCP commands can land in the mirror; check the project root first (HANDOFF §5).
8. **Measure the cost at the rung's worst case.** Flooding cost 0.55 ms per fixed step with 26 spaces wet; per-space opening lists and three other fixes brought it to 0.27 ms (ROADMAP skip / fail log).

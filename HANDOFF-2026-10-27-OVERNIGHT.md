# Overnight handoff, 2026-10-27

**Job of this document:** what the overnight run (Claude, alone, about 00:30 to 08:00) did, what it left for SIX, and
where everything is. Read it first this morning; the module ROADMAPs carry the detail.

SIX's brief: finish Phase 3; test dogfighting extensively with many aircraft; fix the mouse-and-keyboard handling;
run a player with three wingmen against four enemies; write an aircraft skill; then work alone until 8 am, keeping
the docs and git current; no missile work.

## In plain words

- **Phase 3 (AI pilots) is complete.** The AI flies a route, holds fingertip in a four-ship, swaps seats with you,
  goes home and lands after the overhead break, and fights at three skill levels with up to eight jets. Red hunts
  your jet; your wingmen fight on their own and rejoin.
- **The controls fix:** let go of the keyboard or pad and the jet now holds level flight (LEVEL HOLD on the HUD);
  the mouse aim snaps level within 2°.
- **The 4 v 4 is in the menu:** open Dogfight, then **Deep Water → RH Aviator → Add 4 v 4 Dogfight to the Open
  Scene**, press Play. Tab moves you between your jets; H sends the AI home. No weapons: the score is seconds in
  the enemy's rear cone.
- **The aircraft skill** is `.claude/skills/aircraft-workflow` (working with, modifying, building, configuring and
  testing the aircraft).
- **After the aircraft, the radar.** RH Radar Suite gained an engine-free physics core: the radar equation,
  detection statistics, the radar horizon, measurement errors, sea clutter, and a tracker (α-β filter plus a track
  file). It has 37 tests and one end-to-end chain test. **The old radar modules do not use it yet**: that rewrite
  waits for your go.
- **One more aircraft fix:** wingmen joining from crossed starts used to fly through each other (0 m). They now
  avoid each other (23 m at worst).
- **Nothing was flown by a human.** Everything was tested headless, plus play-mode smokes in Dogfight.

## Decisions waiting for you (priced)

| # | Question | Where | Cost |
|---|---|---|---|
| 1 | **A9:** does the rear-cone score require the attacker to point at the target? (Proposed: yes) | `Assets/RH Aviator/BLUEPRINT.md` Q3 | 1 min |
| 2 | **Slice 3f, the fight in the vertical:** build it? Equal-skill pilots stalemate today (METHODOLOGY C44) | `Assets/RH Aviator/ROADMAP.md` | 1 min to decide |
| 3 | **Radar Suite legacy modules:** rewrite the five LOD modules onto the new core, leave them, or retire them later? (Leaning: leave them; General Quarters is the first real user) | `Assets/General Quarters/RADAR-L1-QUESTIONS.md` RS1 | 1 min |
| 4 | **General Quarters Radar L1:** eight questions, R1–R8, with leanings | `Assets/General Quarters/RADAR-L1-QUESTIONS.md` (BLUEPRINT Q6) | 10 min |
| 5 | **Two radar plans:** `Assets/RH Radar/` is an empty greenfield module (README, a September routine ROADMAP, an asmdef) beside `RH Radar Suite`, where tonight's core lives. Which is the home? (Leaning: the suite, and strike the `RH Radar` plan with a reason) | `RADAR-L1-QUESTIONS.md` R1 | 1 min |
| 6 | **Red cannot catch a hands-off player past Mach 1:** red chases at most its fighting speed + 150 kt. Let red chase flat out when the target runs? A gameplay call, so not changed | `Assets/RH Aviator/ROADMAP.md`, slice 3d notes | 1 min |
| 7 | **The three autoroutines never ran.** `.claude/autoroutines/` (missile, radar, aviator; imported 2026-09 in 8181c10a) has no scheduled task and no `auto/*` branch exists. As written they would also `git checkout` in the shared checkout, push to branches outside the 2026-10-24 branch table, and point the radar routine at the empty `RH Radar` module. Retire them with a reason, or redesign them? (Leaning: retire; doctrine guard "a routine without a scheduler is a wish") | `.claude/autoroutines/` | 2 min |

## Things to check

- **`Assets/_SCENES/Dogfight.unity` has an uncommitted change I did not make.** It was saved at 01:01:29 (about
  2,000 lines differ from git), while I was working elsewhere. I left it alone, uncommitted. Before that I backed
  up the then-dirty scene to `Temp/Dogfight.pre-claude-reload-backup-20261027-0150.unity`. If the change is yours,
  commit it; if not, `git checkout -- Assets/_SCENES/Dogfight.unity` discards it.
- **HDRP settings and ProjectSettings** (`DefaultSettingsVolumeProfile.asset`, `HDRenderPipelineGlobalSettings.asset`,
  `GraphicsSettings.asset`, `QualitySettings.asset`) are also modified and uncommitted. They were not mine either.
- The Editor is back on `Assets/General Quarters/Examples/Scenes/GQ_Range.unity`, not in play mode,
  `runInBackground` restored to false.

## Verdict ledger

| Thing | Verdict | Evidence |
|---|---|---|
| Phase 3 (3a–3e) | CHAMPION | 135/135 Air Wing + Aviator tests; every mode smoked in Dogfight |
| Level hold (W54) | CHAMPION | released at 121° of bank, level in 15 s, then within 10 ft and 0.5° for 60 s |
| The 4 v 4 | CHAMPION (headless and play smoke) | no crash; blue 150.2 s, red 13.8 s in the cone; closest pass 146 m |
| Equal-skill fights | FAILED (open) | line v line and ace v line: 0 s both ways in 20 merges each (C44) |
| Yo-yos | FAILED, removed | ace v rookie fell from 41–65 s to 27 s; fights down to 700 ft |
| Join collision fix | CHAMPION | closest 29/23/41/32 m from four starts (was 29/0/41/0) |
| Radar pure core | CHAMPION (pure core only) | 37/37; the chain test holds a closing jet within 60 m and 15 m/s from 60 km |
| Radar core in the LOD modules | NOT STARTED | waits for SIX (CLAUDE.md: confirm a rewrite beyond one file) |

## Hard-won findings (do not re-learn these)

- **The radar roadmap's "sea-skimmers at 200 km" was wrong:** the 4/3-Earth horizon for a 20 m mast and a 5 m
  target is 27.6 km (METHODOLOGY C46).
- **2 of 3 track confirmation with a 700 m/s initiation gate confirms false tracks** once clutter is denser than
  about one plot per 40,000 km² per look; Phase 4's clutter will need a tighter rule.
- **Collision avoidance ran only in a fight**, while joins converge too; and its tie-break sent both wingmen the
  same way (METHODOLOGY AP34).
- **A byte-identical rebuild keeps the old DLL timestamp:** wait on a new symbol or the Bee artifact, not the DLL
  time (unity-mcp-workflow skill).

## Git

- Every working branch (main, Radar-Suite-Dev, feat/general-quarters, feat/aircraft, feat/environment-maps,
  Guided-Fury) is levelled to the same commit after each slice; Guided-Fury through PRs (#41 onward tonight,
  #50 the latest so far). Nothing force-pushed; the archive branch untouched.
- Commits tonight, oldest first: Phase 3 slices (see the RH Aviator ROADMAP change log), then the radar:
  1361552a (radar equation), 840bf7ad (α-β), df24fb14 (track file), e5132a0e (measurement errors), b053aa1f
  (READMEs), 26bd558d (clutter), 7f6372ed (range-rate, C45/C46), c8e62aa4 + c12066e0 (Radar L1 questions), then
  61594723 (join collision fix).

## Tests

| Suite | Count |
|---|---|
| Air Wing + RH Aviator EditMode | 135/135 |
| RH Radar Suite EditMode | 37/37 |

## Next steps (when you say go)

1. Answer the table above (about 20 minutes in all).
2. If R1–R8 are answered: General Quarters Stream A, Radar L1 (+ Tracks L1 if R4 = B).
3. If slice 3f is a go: the vertical fight (energy states, nose-high and nose-low turns).
4. Fly the 4 v 4 yourself; your feel for the level hold is the one thing no test can give.

## Glossary

- **Fingertip:** a close formation; each wingman holds a slot beside and just behind the leader.
- **Rear cone:** the 30° behind a jet's tail, inside 1 km: the score counts seconds an enemy spends there.
- **Pure core:** code with no Unity engine calls, so it runs in plain tests and replays exactly.
- **α-β filter:** the simplest tracker: it smooths each new position against a constant-velocity prediction.
- **M of N:** a tentative track is confirmed once it is seen M times in its first N looks.
- **SNR, CNR, SCNR:** signal-to-noise, clutter-to-noise, and signal-to-clutter-plus-noise ratios, in dB.

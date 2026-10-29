# RH Aviator — Roadmap

**This document's one job:** what to do next for the AI pilot, and what is done. Why each piece exists is in `BLUEPRINT.md`; the decisions it carries (W36–W51) are in Air Wing's register.

**Rules (as Air Wing, General Quarters, Guided Fury and Radar Suite):**
- Slices MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every slice is tested headless in EditMode against limits **fixed before tuning**, then smoked once in Dogfight (W50).
- Every performance claim carries its A/B number (`unity-perf-ab`).
- Before a slice is built, its items are checked against the safe limits (BLUEPRINT §6.5).

**Branch:** `feat/aircraft`, kept level with `main` (CLAUDE.md, Branches), commits through `commit-to-branch`.

**Where things stand (2026-10-27): PHASE 3 COMPLETE.** The AI flies a route, holds fingertip in a four-ship, swaps seats with the player, goes home and lands after the overhead break, and fights (rookie, line, ace) at up to eight aircraft, red hunting the player. 135/135 tests (with the join collision fix, 2026-10-27). SIX 2026-10-27: score by position alone (A9 declined), red's chase stays capped (A10), slice 3f is a go (A11). Next: slice 3f.

**And (2026-10-29): training slice T3a built.** Two squadrons of four learned pilots fight each other in `Assets/_SCENES/Dogfight Training.unity`, respawning at points 20 km apart, trained by `mlagents-learn` with self-play (SIX's picks, TRAINING-CONCEPT M21–M28; BLUEPRINT A13–A15). Nothing is trained yet beyond a five-minute smoke.

## DONE — finished and committed

| Done | What you get | Date | Commit | Tests |
|---|---|---|---|---|
| Blueprint and roadmap | Phase 3's 16 decisions carried for the AI side; slices 3a–3e with their pass criteria | 2026-10-26 | eaf7280f | — |
| Slice 3a — the AI seat, the hands and the gates | An AI pilot that flies Dogfight's four gates through the player's own stick: the mode machine with its logged rules, the shared flight-path layer, the line pilot's profile, an "Add AI Jet" menu, the AI budget met | 2026-10-26 | `92eaaaff` | 6 here, 4 in Air Wing (113 in all) |
| Slice 3e — Phase 3 review | Every mode smoked in Dogfight; the AI budget at eight; the dogfight survey; the controls fix (W54); the aircraft skill; Phase 3 closed | 2026-10-27 | see the change log | 134 in all |
| Training T3a — two learned squadrons that fight each other | The learned seat (the ace's eyes and hands, 68 senses, 4 actions and the trigger), the referee's reward, Air Wing's arena (respawn, collisions, the box), a spectator camera, explosions, the Dogfight Training scene with blue and red squadrons and a pilot setting each, the training director, three trainer configs, Python 3.10 and `mlagents` 1.1.0 | 2026-10-29 | `3dde2e84` | 8 here, 9 in Air Wing |
| Missiles for every pilot, 8 v 8 (A16) | Rule pilots launch on a lock inside their range (rookie 5 km, line 3.5, ace 2.5), one at a time; the learned pilot's pickle and its missile senses (version 2, 88 numbers); the training scene at 8 v 8 | 2026-10-29 | see the change log | 3 here (MissileFightTests) |
| Slice 3d — Pilot L1, the fight | Perception (field of view, reaction delay, memory), pursuit (lead, pure, lag), the break turn, energy sense, turn matching; rookie, line and ace; the engagement, red hunting the player and blue's wingmen fighting and rejoining; rear-cone scoring; the AI budget met at eight | 2026-10-27 | `8e4ee725` | 5 here (130 in all) |
| Slice 3c — going home and the break | The AI flies home on the player's order or at the bingo stand-in, the overhead break, the pattern and a landing on 09/27; the "go home" order (A3) | 2026-10-27 | `ec2c5193` | 3 here (125 in all) |
| Slice 3b — formation and seats | An AI wingman that joins and holds fingertip on its leader; the seat cycle key and the HUD pick list; the player takes any jet mid-flight and the AI takes it back; an "Add AI Wingman" menu | 2026-10-27 | `a2ae171d` | 4 here, 4 in Air Wing (122 in all) |

## STILL TO DO

### Next
- **Next for training: the first real round** (TRAINING-CONCEPT T3–T5). Run `Training/config/fighter_selfplay.yaml` for as many hours as SIX allows (concept open question 2), watch TensorBoard, and judge the policy by kills, crashes and pointed cone time against the rule pilots (`fighter_vs_rules.yaml`), not by its mean reward. Before long rounds: a headless player build (several in parallel), recordings of the first, middle and last fights (M8, M18) and the training theatre (M12). The gate (M11) waits for those.
- **Next: slice 3f — fight in the vertical** (A11, SIX go 2026-10-27): energy states, nose-high and nose-low turns, the ace spending its G, so equal fights resolve. Spike first; pass criteria fixed before building: line v line and ace v line each produce a winner in some of 20 merges **by pointed cone time** (the A9 measure as a diagnostic; see below), and no fight sinks to the floor. *Survey with A9 removed (2026-10-27):* line v line 14–6, 6.1 / 5.6 s a fight (was 0 / 0); ace v line 14–6, 8.5 / 7.0 s (was 0 / 0); ace v rookie 19 of 20, 1278 s to 60 s (was 1217 to 0); the 4 v 4 player fighting 63.4 / 88.4 s (was 2.8 / 41.9). Nearly all the new equal-fight time is head-on passes, so the position score cannot judge 3f.

### Left open in finished slices
- **Training T3a** (2026-10-29): the policy is untrained: in the smoke it flew out of the box 9 times in 5 minutes and emptied its gun at random. The scripted stand-in (no trainer, no model) chases with lead pursuit but never fires at the ace's 6 mils (A13, aircraft-workflow trap 16). Not built: recordings and the theatre (M12), the Director (M15), terrain (M13), fuel, the radar picture, MA-POCA (M10), the headless player build, the held-out gate (M11). Leaving the box plays no explosion (only kills, collisions and impacts do). The smoke ran in the Editor at about 3× real time with HDRP drawing; a headless build will be faster, not measured.
- **Slice 3a:** the first approach to gates 2 and 3 goes around once (each 90° turn onto a gate's line does not fit the first try); the course takes about 5.5 minutes. Not flown against a player by hand.
- **Slice 3d, after the dogfight survey** (`FightTests.Survey`, explicit; 2026-10-27): range control (close with speed, fight at the fighting speed) took the ace from 1.7 to 41–65 s a fight against the rookie, ahead in 17–20 of 20. **Equal or near-equal pilots stalemate:** line v line and ace v line score 0 s both ways in 20 merges each; the pursuit holds the target's height, so the extra G an ace has is never spent (a level 75° turn is ~4 G either way). A first try at the vertical (yo-yos) made it worse (ace v rookie 27 s, fights down to 700 ft) and was removed. A hands-off player at a 0.8 lever accelerates past Mach 1 and red, chasing at most its fighting speed plus 150 kt, never catches it (kept so by SIX, A10: running away is a valid escape). Collision avoidance (100 m in 3 s) keeps passes to 70–140 m in fights; formation joins could still pass through each other (fixed the same night, below). Not yet flown against a human. *Next for the fight: an energy-based vertical (a proposed slice, below).*
- **Slice 3c:** every one of the 20 landings is the same landing once on the initial (10.2 ft/s at about 257 m, stopped at about 1,219 m): the pattern is deterministic and the starts only change the way in. Ordered home from the wrong side, the pilot goes around the initial first (in the Dogfight smoke, two minutes). No wind exists yet. The pattern was tuned on the L1 jet at 60 % fuel.
- **Slice 3b:** the formation gains are tuned on the L1 jet at 5,000 ft and 350 kt; the path gain sits 1.6× inside where the hold rings (METHODOLOGY C42), so another level, weight or speed needs the sweep again. Joining from ahead of the leader is not tested (the wingman slows to 250 kt and lets it pass). The AI budget was not re-measured for formation (slice 3e measures four aircraft). Not yet flown by a human on the wing.

---

# Slices (Pilot L0, then Pilot L1; W49)

### Slice 3a — the AI seat, the hands and the gates (W36, W37, W39, W44, W51) (done 2026-10-26: all pass criteria met; 113/113 tests in both modules, `92eaaaff`)
- [x] Mined the legacy flight code (A6), read-only, into `_notes/legacy-mining.md`: **nothing worth carrying.** `AI_FlightController` is MouseFlight's camera rig, `FlightControllerManager` a status-light board, the JMAC agents a homing weapon (out of scope).
- [x] Air Wing side (A2): **the hands**, `Core/Seats/FlightPathHands`, beside `MouseInstructor`. *Not built on its `Steer`* (A2 refined): `Steer` has no bank limit (Air Wing AP25). The hands fly an aim point at an altitude and a speed:
  - the aim → a sideways acceleration by the **L1 path-following law** (a = k·V²·sin η / L, k = 2, L capped at the turn radius) → a bank, capped;
  - the altitude error → a flight-path angle, capped; the path and the bank → G (cos γ / cos φ plus a correction), capped by the pilot's G;
  - the speed → throttle, proportional plus integral (no wind-up), afterburner and speed brake;
  - the ground guard: under 100 m, or heading there within 6 s, it climbs at 8° with the bank held to 30°.
- [x] Air Wing side (A1): **the picture**, `FlightCore.Picture`, every aircraft as the previous tick ended, the same for every seat; and `FlightCore.Surface`. Air Wing's banned-API scan is now a public `Scan(folder)`, which RH Aviator's core runs too.
- [x] `DeepWater.Aviator` references `DeepWater.AirWing`; `Core/` passes the banned-API scan.
- [x] `AviatorSeat : ISeat` (`SeatKind.Ai`): the watch at 4 Hz on the tick count (W44), the mode machine with the rule log (W36). Modes Navigate and HoldCourse. Rules: **next gate**, **course flown**, **go around** and **back on the line**, each logged with its tick and a reason in words; `AviatorPilot` also prints them to the console.
- [x] `PilotProfileSO` (W45) and the **line** pilot (`ScriptableObjects/Pilot_Line.asset`, written by **Deep Water → RH Aviator → Apply Line Pilot Profile** with its provenance; all AUTHORED).
- [x] Navigate flies Air Wing's `GateCourseSO`, gate by gate: a carrot on the gate's line, at least a turn radius and at least the offset ahead; a sticky go-around, back down the line and out to its side, when too close to line up or past the gate; "lined up" means its present track would cross inside 70 % of the ring.
- [x] Scene: **Deep Water → RH Aviator → Add AI Jet to the Open Scene** puts an AI F/A-18E (aircraft 2, the line pilot, the Dogfight course) 80 m right of and behind the player's jet, unsaved. The Dogfight scene on disk keeps one jet (W51).
- [x] **Found and fixed while tuning** (the pass criteria stayed as fixed):
  - A carrot fixed at the gate's entry point cut onto the line at 90° on a 1.7 km turn and missed the 100 m ring; then, past the gate, it **orbited for ever**, its aim point inside its own turning circle (METHODOLOGY AP31).
  - A carrot shorter than the turn radius (400 m against 1.2 km) **swung across the line**: −2,500 → +309 → −124 m. A bank proportional to the heading error kept a ±40–77 m swing that never settled. The L1 law with its look-ahead about one turn radius settled it: gates now within 4 m of the centre (METHODOLOGY C41).
  - The go-around, decided afresh each watch, **flipped** at its boundary; it is sticky now, and logged.
  - "Lined up" first tested a fixed offset at one moment; it now asks where the present track crosses the ring.
  - The watch fell a tick late (1201 for 1200): the core's tick is `0.02f`, 0.0199999995 s as a double; the watch rounds it to the microsecond (METHODOLOGY AP26 again).
  - A far aim (a heading) gave almost no bank once the error fell under 90°: L is now capped at the turn radius.
  - Speed held 23 kt fast on a proportional throttle: an integral term took it out (400.0 kt held, 30 s).
- [x] **Pass criteria, met** (headless, from Dogfight's start, every part L1, land height 8 m everywhere):

  | Gate | Flown | Off centre |
  |---|---|---|
  | 1 · 5,000 ft (350–450 kt) | 399 kt, 5,000 ft | 0 m |
  | 2 · DDG pass (800–1,200 ft) | 353 kt, 1,001 ft (after one go-around) | 3 m |
  | 3 · climb to 6,000 ft (≥ 300 kt) | 361 kt, 6,001 ft (after one go-around) | 4 m |
  | 4 · runway pass (300–700 ft, 250–350 kt) | 306 kt, 512 ft | 4 m |

  - all four in order, each inside its bands; course flown in 335 s; no crash; lowest 659 ft away from gate 4's run (511 ft on it);
  - the rule log names every mode change with its tick and reason, on a watch;
  - two runs: the same Fix on every tick, the same rule log;
  - **the AI's own cost, player build (Windows, Mono, release; i9-12900H; two runs): 2.97 and 2.71 µs per pilot per tick with 1 pilot, 4.01 and 3.55 µs with 4: budget 10, met.** Measured by `AviatorBenchmarkRunner` (**Deep Water → RH Aviator → Build Benchmark Player**, `AviatorBenchmark.exe -batchmode -nographics -aviatorBenchOut <file>`): the same aircraft on hold seats against AI pilots, the difference. *Unexplained:* the hold-seat jets here cost 27–30 µs per aircraft per tick, against 18 µs in Air Wing's own benchmark (5,000 m, a different start); both inside Air Wing's 50 µs. *Narrowed 2026-10-27 (Editor A/B, a RunCommand, four hold-seat jets, 3,000 ticks after 300, two rounds): Air Wing's start (5,000 m, sea at 0, 500 m apart), this benchmark's (1,524 m, surface at 8 m, 300 m apart) and the two crossings cost the same, 20.3–21.5 µs. The start conditions are ruled out; the gap lies in the player harness (run order, warm-up, CPU state), not yet measured.*
- [x] Air Wing tests (`FlightPathHandsTests`, 4): the hands hold 400.0 kt, height and track for 30 s; a turn behind stays inside the bank and G caps and comes round; the ground guard keeps it 110 m up when asked for the sea; aircraft 2 sees aircraft 1 where the last tick left it.
- [x] **Smoke in Dogfight** (play mode, the AI jet added unsaved, the sea and land of the scene, PhysX contacts): it flew all four gates clean, 3, 2, 36 and 12 m off centre, with one go-around at gate 2, and went into HoldCourse at 239 s. The scene was then reloaded from disk.

### Slice 3b — formation and seats (W40, W42) (done 2026-10-27: all pass criteria met; 122/122 tests in both modules, `a2ae171d`)
- [x] Modes **Join** and **Fingertip** (W40) on the order `JoinOn(leader)`; also the orders `FlyRoute` and `HoldCourse`, each logged as a rule. Rules: **in the slot** (inside 10 m on every axis, closing under 5 kt), **lost the slot** (over 150 m), **leader lost** (it crashed). The slot: 40 m right (about three of the F/A-18E's 13.6 m wingspans), 10 m back, level with the leader, in the leader's level frame (`Core/Formation`, `Slot`).
- [x] The plan is worked out **every tick** from the picture (A7, locked): the slot's speed plus an overtake (linear near the slot, never more than √(2 a d) at 1.5 m/s², at most 150 kt), with a speed lead and the speed brake past 8 kt over; the aim a look-ahead along the slot's track; the leader's **bank, roll rate, flight-path angle and rate as feed-forward** (AP32), the rates three-quarters from how it is rotating and a quarter from its G. Formation flies its own hands, the route hands with tighter height and path gains (`PilotProfile.FormationHands`).
- [x] Back from the player (W42): the seat sees the gap in ticks, judges nothing across it, starts the throttle where the player left it (`FlightPathHands.Resume`) and decides at once.
- [x] Air Wing side (A8, locked): **`Core/Seats/SeatRoster`**: one player seat and each aircraft's own seat; `Cycle()` steps through the aircraft not crashed, in id order; the jet left goes to its own seat, or holds the player's last inputs. `PlayerSeat.TakeOver` puts the lever at the jet's throttle and keeps its switches. `ISeatStatus` lets a seat describe itself. The **cycle key**: Tab and the Xbox D-pad right (`CycleSeat` in `AirWingFlight.inputactions`), applied between ticks. The **chase camera and the HUD follow the player's aircraft**; the HUD's **pick list** (top right) shows who flies what.
- [x] Air Wing's hands take optional feed-forward (bank, roll rate, flight-path angle and rate; zero for a route, so slice 3a flies bit for bit as before) and `Resume(throttle)`; the throttle integral is held to ±1, not ±0.5 (AP33).
- [x] Scene: **Deep Water → RH Aviator → Add AI Wingman to the Open Scene**: an AI jet 600 m behind and 200 m right of the player's, ordered to join its wing, and an AI pilot in the player's jet holding course, so whichever jet the player leaves has someone flying it. Unsaved.
- [x] **Found and fixed while tuning** (the pass criteria and the leader's manoeuvre stayed as fixed):
  - The join was planned on 2 m/s² of deceleration; an idle jet near 400 kt loses about 2.3 and less as it slows, the speed loop lagged by 20–30 kt, and the wingman slid **51 m past its leader**. Now 1.5 m/s², a speed lead and the speed brake: the closest it comes is 9.9 m behind.
  - The throttle integral sat on its ±0.5 stop (trim 0.75, needed 0.22): **5–7 m out of the slot** for good (AP33).
  - Aimed on the slot's predicted arc, the wingman **over-rolled** with every change in the leader's turn (+22° then −15°) and drifted 11.8 m across; with the leader's bank and roll rate fed forward, 2.3 m. A bank worked out from the turn rate (tan φ = V ψ̇ / g) asked 22° for the leader's 7° in its banked pull: it holds only in a level 1 G turn.
  - Reading the leader's climb from its G, the wingman fell **10 m behind the pull**; from its rotation alone it pulled 2.6 G for 2.13 and ended 7 m high. Three-quarters rotation, a quarter G.
  - A 0.3° per metre height gain set up a **porpoise**, −1.7 to +6.5 G. The final gains came from a sweep, 100 runs of the fixed hold scenario (`FormationTests.Sweep`, explicit): 0.03° per metre, 0.5 G per degree, which sits 1.6× inside where the hold rings (C42).
  - The test leader's turn hands started with a fresh throttle integral and lurched 10 kt: they now pick up the throttle (a test fix).
- [x] **Pass criteria, met** (headless, every part L1, 5,000 ft, 350 kt, a flat sea):

  | Criterion | Limit | Flown |
  |---|---|---|
  | Join from 2 nm behind | ≤ 90 s, never past the leader | in the box at 76.7 s; at most 9.9 m behind the leader |
  | Hold through a 30° bank turn (leader to 30.8°) and a 2 G pull (leader to 2.14 G), 120–260 s | ±10 m along, ±5 m across, ±5 m up | 6.8 m, 2.3 m, 4.2 m |
  | Hand the wingman's jet to the player at 100 s and back at 110 s | one entry per aircraft per tick; the Fix continuous | 2 per tick throughout; the worst tick 3.9 mm off its velocities; the throttle 0.187 → 0.187 both ways |
  | Two runs | the same rule log and Fix | the same |

  - the hold's trace is written to `Temp/aviator_3b_trace.csv` on every run;
  - Air Wing tests (`SeatRosterTests`, 4): the cycle in id order, wrapping; the jet left goes to its own seat or holds; the player takes over at the jet's throttle; a crashed jet is skipped, and the pick list's words.
- [x] **Smoke in Dogfight** (play mode, the wingman added unsaved, the scene's sea and land, PhysX contacts): the wingman was in the slot about 36 s after the start and held it to 0.0–0.3 m while the hands-off player jet drifted up to 674 kt (Mach 1.05). At 100 s the seat cycle put the player in the wingman's jet; the player's jet went to its AI, holding course; the pick list read "1 AI · HOLD COURSE", "> 2 YOU". At 115 s the cycle gave it back; the AI was 97 m off the slot and back within 1 m 29 s later, without losing the slot. The scene was then put back from disk and GQ_Range reopened.

### Slice 3c — going home and the break (W41, W47) (done 2026-10-27: all pass criteria met; 125/125 tests in both modules, `ec2c5193`)
- [x] Return on the player's order (A3: `Modules/AviatorOrders`, H or the Xbox D-pad up, every AI pilot not in the player's jet) or at the bingo stand-in (W47: `BingoFlightTimeS`, logged "a flight-time stand-in for bingo fuel"; 0 = never). Home is a `Strip` (`AviatorPilot.homeRunway`); the pilot picks the end whose initial is nearer when it turns for home.
- [x] The break, as legs on the hands (W41, `Core/Pattern`): **initial** (Navigate's line-following and go-around on a gate at the break point: 800 ft, 350 kt, 3 nm), **break** (a level left turn at the pilot's 70° bank, about 3 G, slowing to 240 kt; *not* the roadmap's "about 4 G": the hands' bank limit sets it), **downwind** (600 ft, 1,500 m abeam, gear and flaps below 250 kt, slowing to 145 kt), **the 180** (a descending left turn at 40° onto the centreline), **final** (the 3° path to a point 300 m in, a flare from 30 ft to 0.8°), **touchdown**, **rollout** (idle, the nose's fall checked, brakes once it is down, the pedals on the centreline), **stopped**. Each leg change is a logged rule. The slow legs fly the *pattern hands* (`PilotProfile.PatternHands`: 40° bank, a lower path gain, a 6° dive limit, a 30 m floor); the ground guard stands down on the final only (`FlightPathRequest.Landing`).
- [ ] **Pass criteria, fixed now:** from 10 nm out, lands on runway 09/27 with touchdown sink < 12 ft/s, on the centreline within ±5 m, stopped on the runway; each segment within its own height, speed and heading box; no crash in 20 runs from scattered starts.
  - *The boxes, fixed 2026-10-27 before any tuning* (heights above the runway; a leg's first 10 s are its entry and not boxed): **initial** 650–950 ft, 320–380 kt, track within 10° of the runway; **break** 600–1,000 ft, no more than the pilot's G; **downwind** 450–950 ft, track within 15° of the reciprocal, under 250 kt by its end; **turn to final** 150–950 ft, bank within the pattern's limit plus 10°; **final** within 75 ft of the 3° path down to 100 ft, 135–160 kt, track within 10° of the runway; **touchdown** in the first half of the runway; **rollout** stopped inside the runway's length and width.
  - *The scattered starts:* 20 seeded starts, 10 nm from the runway's centre on a random bearing, a random heading, 3,000–10,000 ft, 300–450 kt.
- [x] **Found and fixed while tuning** (the criteria and boxes stayed as fixed; the turn to final's box was added before tuning):
  - The break first asked for the approach speed at 70° of bank: the jet came round at 167 kt and 31° of attack and **stalled** onto downwind. The break now slows to 240 kt; the rest comes off on downwind.
  - In the turn to final the route's path gain (0.25 G per degree) **pushed to −0.2 G and dived at −24°**: at 145 kt a G moves the path 2.4 times faster than at 350 kt (METHODOLOGY C43). The pattern hands use 0.1, a 6° dive limit, and keep the ground guard at 30 m.
  - A carrot of a turn radius (680 m) on the final left the jet **44 m off the centreline** at 700 m out and touching down 10 m off; the final's own carrot is 300 m.
  - The rollout's stick, copied from Air Wing's landing test (aft in proportion to pitch), **rotated the jet again after the flare at 141 kt and struck the tail**. It now only checks the nose's fall.
- [x] **Pass criteria, met** (headless, every part L1, the ground everywhere at the runway's height, 20 seeded starts 10 nm out, 3,000–10,000 ft, 300–450 kt, any heading): all 20 stopped on the runway, none crashed; touchdown 10.2 ft/s (limit 12), 254–258 m past the threshold, 0.0 m off the centreline (limit 5); stopped 1,215–1,221 m down, 0.0 m off; every leg inside its box. The rule log names every leg in order; bingo fires on the tick count, labelled a stand-in.
- [x] **Smoke in Dogfight** (play mode, the wingman added unsaved, PhysX contacts): joined, ordered home at 45 s, went around once for the initial, then initial, break, downwind, the 180, final; touchdown at 8.4 ft/s, 0.0 m off; stopped 1,225 m down 27 at 330 s. The scene was put back from disk and GQ_Range reopened.

### Slice 3d — Pilot L1, the fight (W38, W43, W45, W46, W52, W53) (done 2026-10-27: all pass criteria met; 130/130 tests in both modules, `8e4ee725`)
- [x] Perception (`Core/Perception`, W38): a ring of past pictures, so the pilot sees each aircraft as it was its reaction delay ago, and only inside its field of view and visual range; a lost contact is remembered where last seen, carried on at its velocity, and forgotten after the memory time.
- [x] Fight (`Core/Fight`, W45): lead, pure or lag pursuit by the geometry and the closure; a break turn when a seen enemy, close in the pilot's rear quarter, is pointing at it; turn matching (the target's turn fed forward, AP32); energy sense (below the fighting speed the G and bank are eased, down to 2 G at the slow speed); the profile's G; a fight floor. Lost sight: back to where it was last seen ("search").
- [x] The engagement (W52, W53): sides; **red hunts the player's jet** (a controller's "vector" from the truth until it sees it, labelled in the rule log); blue goes for the nearest red it sees and, with none inside its clear range, is "clear" and rejoins the player's wing. Rules: tally, engage, break, tail clear, vector, search, clear.
- [x] Rear-cone scoring (W46): seconds in an enemy's rear cone (30°, 1 km), and the fight log of every entry and exit. ~~*The attacker must also point at its target* (A9, proposed)~~: declined by SIX 2026-10-27; scored by position alone, as W46 reads (a head-on pass scores a few seconds each way; accepted). Not yet on the HUD (slice 3e). No weapons, no gun solution (BLUEPRINT §6.5).
- [x] Profiles (W45): **rookie** (sees 90°, 1.2 s late, 4.5 G, 60° bank, pure pursuit only, fights too fast, no energy sense, no turn matching), **line** (120°, 0.7 s, 6 G), **ace** (150°, 0.35 s, 7 G, 75° bank, lead and lag, full turn matching). **Deep Water → RH Aviator → Apply Pilot Profiles** writes all three with their provenance (AUTHORED: a game's skill numbers).
- [x] Up to eight aircraft (W43 re-decided by SIX 2026-10-27).
- [x] Air Wing's hands take a per-request G and bank limit (`FlightPathRequest.GLimit`, `BankLimitDeg`).
- [x] After the Dogfight 4 v 4 smoke (all three blue AIs piled onto one red jet and sat in its cone for three minutes): **blue spreads its targets** (`Engagement.Claim`: the nearest red no teammate is after); a join from below waits for speed before it climbs (a hard switch at 250 kt cycled; a fade from 250 kt settled in a 6° climb at 259 kt); the tight formation hands fly only within 500 m of the slot (on a 28 km join they porpoised). The headless 4 v 4 is now a fight: blue 103.7 s, red 126.5 s, 39 cone events; the wingmen back in their own slots 171 s after red left.
- [x] After the first tests (2026-10-27, the dogfight testing SIX asked for): **range control** (`RangeKtPerM`, `AttackRangeM`, `ChaseOverKt`; the rookie has none), **collision avoidance** (`AvoidM`, `AvoidS`, rule "avoid"), "target gone" when a target leaves the fight, a slow join from below levels off first, and **flight slots** (`JoinOn(id, leader, position)`: 1 left, 2 right, 3 outside right; three wingmen had all flown the one right-wing slot). Scene: `Modules/AviatorFight` (the engagement, scoring after every tick, a score panel: blue and red seconds, "ON 6'S TAIL" / "6 ON YOUR TAIL"), `AviatorPilot` task Fight with a side and slot, **Deep Water → RH Aviator → Add 4 v 4 Dogfight to the Open Scene**.
- [x] **Found and fixed:**
  - Perception kept an entry for an aircraft never seen, so "known" answered yes for it (the dead-astern test caught it).
  - After a head-on pass the rookie forgot the ace behind it and **flew straight on at 416 kt** for the rest of the fight: a lost target is now searched for where it was last seen.
  - The ace **broke against a jet flying away** from it: a threat must now be pointing at the pilot.
  - At 75° and 7 G the ace **bled to 200 kt and sank 4,000 ft**: energy sense eases the turn when slow.
  - On the L1 law alone the ace **settled 33° off a turning rookie's tail** at 1.5 km, turning no harder: the target's turn is now matched by feed-forward.
- [x] **Pass criteria, met** (headless, every part L1):

  | Criterion | Limit | Flown |
  |---|---|---|
  | Ace against rookie, 20 neutral merges (head on, 6 km, 9,000–11,000 ft, 380–420 kt, 180 s) | ace ahead in ≥ 15; no crash | 20 of 20 (ace 34 s in all, rookie 0); lowest 8,937 ft; no crash |
  | A pilot with the player outside its field of view | no turn before its delay | in view at tick 2046, tally at 2088 (delay 35 ticks); 0.0° of bank before |
  | 4 v 4 (player jet and three wingmen, line; red rookie, two line, ace), 300 s, then red leaves | no crash; red goes for the player; blue engages, then rejoins | no crash; every red went for aircraft 1; every blue wingman engaged and was back in fingertip at 390 s; red 104.7 s, blue 5.3 s in the cone |
  | The AI's cost at eight, player build (Windows, Mono, release, two runs) | ≤ 10 µs per pilot per tick | 2.96 and 3.21 µs (route: 1 pilot 1.51 / 1.42, 4 pilots 1.07 / 1.39) |
- [ ] **Pass criteria, fixed now:** over 20 fights from neutral merges, ace beats rookie on rear-cone time in ≥ 15; no AI crashes into the ground or the sea; an AI with the player outside its field of view does not turn toward the player before its delay has passed.
  - *Added 2026-10-27 before building, for W43 at eight, W52 and W53:* a 4 v 4 (a player jet, three blue wingmen, four red), 5 minutes headless: no AI crashes; every red pilot pursues the player's jet (W52); every blue wingman engages a red jet and is back in fingertip once none is within its clear range (W53); both sides' rear-cone seconds logged. The AI's own cost at eight aircraft ≤ 10 µs per pilot per tick in a player build (A4).
- [ ] Smoke: a 1v1 in Dogfight against each profile. *Moved to the dogfight testing that follows Phase 3 (SIX, 2026-10-27).*

### Slice 3e — Phase 3 review (done 2026-10-27: PHASE 3 COMPLETE)
- [x] **Each mode in Dogfight** (play mode, PhysX contacts, the scene put back from disk after each): the gates (3a: four clean, 239 s); fingertip and the seat cycle (3b: in the slot in about 36 s, held to 0.0–0.3 m to 674 kt; seats swapped both ways); going home and the break (3c: touchdown 8.4 ft/s, 0.0 m off, stopped 1,225 m down 27); the 4 v 4 (3d, the final run: the player's jet handed to its AI at 5 s; blue spread across three red targets; red hunted the player; blue 150.2 s, red 13.8 s in the cone; no crash; closest pass after the start 146 m).
- [x] **The AI budget**, re-measured at eight (W43 re-decided): 2.96 and 3.21 µs per pilot per tick in a 4 v 4, player build (budget 10).
- [x] **The dogfight survey** (SIX: test dogfighting extensively with many aircraft): merges of every pairing, 1 v 2, 2 v 2, the 4 v 4 two ways; its findings changed the fight (range control, collision avoidance, flight slots, target spreading, the join's energy rules) and left one problem open: equal pilots stalemate (C44; slice 3f proposed).
- [x] **The controls fix** (W54, Air Wing): the level hold for keyboard and pad, the mouse's level detent.
- [x] **The aircraft skill** (SIX: a skill for working with, modifying, building, configuring and testing the aircraft): `.claude/skills/aircraft-workflow`.
- [x] METHODOLOGY: AP32, AP33, C42, C43, C44 from Phase 3.
- [x] Air Wing's Phase 3 closed (its ROADMAP).

---

# Superseded plan (the routine roadmap of 2026-09; A5)

Kept for the record. Air Wing now owns the airframe side (W5), and W17, W49 and the safe limits set what the AI does. Each unchecked item is struck through with its reason.

## Phase 0 — Foundation
- [ ] ~~skipped~~ ~~Mine legacy flight code for domain knowledge.~~ *Carried 2026-10-26 into slice 3a (A6).*
- [ ] ~~skipped~~ ~~State + profile structs (`AviatorState`, `AircraftProfileData`: mass, thrust curve, max-G, fuel, weapons loadout).~~ *Superseded:* the aircraft's numbers are Air Wing's `AircraftProfile`; the pilot's are `PilotProfileSO` (W45); no weapons loadout (safe limits).
- [ ] ~~skipped~~ ~~AircraftProfileSO + RoleProfileSO.~~ *Superseded:* Air Wing owns `AircraftProfileSO`; roles are modes (W36); pilot numbers are `PilotProfileSO`.
- [ ] ~~skipped~~ **IAircraftActuator contract.** *Skipped 2026-10-25:* Air Wing's `ISeat` and `PilotInputs` are the contract (Air Wing BLUEPRINT W5, W6). RH Aviator implements AI seats against them.
- [ ] ~~skipped~~ ~~IDecisionMaker contract (world snapshot in, `FlightCommand` out).~~ *Superseded:* the brain reads the picture (A1) and writes a flight-path request to the hands (W36, W37).
- [ ] ~~skipped~~ ~~AviatorEntity pure-C# core.~~ *Superseded:* the AI seat (slice 3a).
- [ ] ~~skipped~~ ~~AviatorBehaviour MonoBehaviour adapter (FixedUpdate pump, binds an actuator).~~ *Superseded:* Air Wing's runner steps every seat inside the flight core; no second pump.
- [ ] ~~skipped~~ ~~EditMode test scaffolding.~~ *Carried into slice 3a.*

## Phase 1 — L0 Waypoint follower
- [ ] ~~skipped~~ ~~WaypointFollowerDecisionMaker (a `WaypointPath` SO).~~ *Carried into slice 3a as Navigate on Air Wing's gate course (W39).*
- [ ] ~~skipped~~ ~~NullActuator + StubAircraft for tests.~~ *Superseded:* tests fly the real flight core headless (W50).
- [ ] ~~skipped~~ ~~EditMode tests: waypoint progression, end of path, distance tolerance.~~ *Carried into slice 3a's pass criteria.*

## Phase 2 — L1 Single-target intercept
- [ ] ~~skipped~~ ~~Sensor input adapter (`ISensorReport`, `ITargetTrack`).~~ *Deferred:* sensors come later (W38).
- [ ] ~~skipped~~ ~~Lead-pursuit intercept geometry.~~ *Carried into slice 3d as positioning for the fight, not an intercept.*
- [ ] ~~skipped~~ ~~Bingo / RTB logic.~~ *Carried into slice 3c (W47).*
- [ ] ~~skipped~~ ~~EditMode tests: intercept convergence, RTB at the right fuel.~~ *Carried into slices 3c and 3d.*

## Phase 3 — L2 Air-combat maneuvering
- [ ] ~~skipped~~ ~~Energy state model.~~ *Carried into slice 3d.*
- [ ] ~~skipped~~ ~~Pursuit selector (lag, pure, lead).~~ *Carried into slice 3d.*
- [ ] ~~skipped~~ ~~Gun snapshot detection.~~ *Dropped:* no weapons and no gun solution (W46, safe limits).

## Phase 4 — L3 BVR + countermeasures
- [ ] ~~skipped~~ ~~Missile launch logic.~~ *Dropped:* safe limits; only if SIX asks.
- [ ] ~~skipped~~ ~~Notch maneuver.~~ *Dropped:* safe limits.
- [ ] ~~skipped~~ ~~Defensive RWR adapter.~~ *Dropped:* safe limits.

## Phase 5 — L4 Multi-ship tactics
- [ ] ~~skipped~~ ~~Wingman role.~~ *Carried into slice 3b as formation, with no shots.*
- [ ] ~~skipped~~ ~~Element / division over a `DoctrineSO`.~~ *Deferred:* W43 caps Phase 3 at four aircraft.

## Phase 6 — L5 Mission-level AI
- [ ] ~~skipped~~ ~~Strike package with SEAD escort.~~ *Dropped:* outside W17 and the safe limits.
- [ ] ~~skipped~~ ~~Tasking interface.~~ *Deferred:* not asked for.

---

## Change log
- **2026-09** — The routine roadmap (Phases 0–6) written; nothing built.
- **2026-10-25** — `IAircraftActuator` skipped: Air Wing's `ISeat` and `PilotInputs` replace it.
- **2026-10-26** — Rewritten from Air Wing's Phase 3 interview (W36–W51): slices 3a–3e with pass criteria fixed before tuning; the old plan kept below, superseded (A5). Nothing built.
- **2026-10-26** — SIX confirmed A1–A6. Slice 3a done: the AI pilot flies Dogfight's four gates through the stick (L1 law hands, sticky go-around, logged rules; `92eaaaff`), 113/113 tests in both modules, the AI budget met (3–4 µs per pilot per tick). Found: an aim point inside the turn orbits for ever (AP31); a look-ahead shorter than the turn radius swings (C41); a float clock moved the watch (AP26).
- **2026-10-27** — Slice 3b done at SIX's go: Join and Fingertip on the leader, the seat cycle and the pick list (Air Wing), 122/122 tests in both modules; the join in 76.7 s, the hold within 6.8 / 2.3 / 4.2 m through a turn and a 2 G pull. A7–A8 proposed (BLUEPRINT Q2). Found: following on position alone is late and overshoots (AP32); a throttle integral clamped short of the cruise setting (AP33); tune by a sweep and ship the margin (C42).
- **2026-10-27** — SIX confirmed A7 (formation flown every tick) and A8 (the seat roster in Air Wing) as written.
- **2026-10-27** — Slice 3c done (SIX: "finish out phase 3"): going home on the order (A3) or the bingo stand-in, the overhead break and the landing on 09/27, 125/125 tests; 20 of 20 scattered starts land at 10.2 ft/s on the centreline. Found: a path gain in G per degree is a loop gain that grows as the speed falls (C43).
- **2026-10-27** — Slice 3d done: Pilot L1, the fight; rookie, line and ace; the engagement (red hunts the player, W52; blue fights and rejoins, W53); rear-cone scoring with A9 proposed; 130/130 tests; the ace ahead in 20 of 20 merges; the AI at eight 3 µs per pilot per tick. The fights are gentle (left open).
- **2026-10-27** — The dogfight survey (SIX: test dogfighting with many aircraft): range control, collision avoidance, flight slots, the fight scene and score panel, the 4 v 4 menu; yo-yos tried and removed; 134/134 tests. Equal fights stalemate (left open; slice 3f proposed).
- **2026-10-27** — Slice 3e: Phase 3 complete. Every mode smoked in Dogfight, the final 4 v 4 (no crash; blue 150.2 s, red 13.8 s), the budget at eight, the survey, W54, the aircraft skill. Open: A9 (Q3); slice 3f (the vertical) proposed.
- **2026-10-27** — Joins avoid each other with no fight. A four-ship join survey found crossed joins meeting at 0 m (avoidance ran only in a fight). Now a join with no fight avoids inside `JoinAvoidM` (25 m, AUTHORED, inside the 40 m slot spacing; checked in `PilotProfile.Validate`) on the route hands; the avoid tie-break, which compared against the leader's id and so sent two level wingmen the same way, now uses the pilot's own (lower climbs). `FormationTests.FourShipJoins_KeepClearOfEachOther_AndAllReachTheirSlots`: four starts, closest 29 / 23 / 41 / 32 m (was 29 / 0 / 41 / 0), all in fingertip in 15–69 s, nobody down. 135/135 with Air Wing.
- **2026-10-27** — SIX's answers: A9 declined (position alone; `RearCone.Inside` loses its pointing check, a test pins it), A10 red's chase stays capped, A11 slice 3f a go.
- **2026-10-29** — Training T3a (SIX: "add 1 red squadron and one blue squadron. Give the planes a machine learning brain and give them the ability to fight each other to train", with respawn points opposite each other; then a spectator camera, then explosions). Eight questions, all answered with the recommended option (M21–M28). Built: `LearnedSeat`, `LearnedSenses`, `LearnedActions`, `RewardLedger` (pure, `Core/Learning`); `FighterAgent`, `FighterTraining`, `TrainingSquadron` (`Modules/Learning`, its own ML-Agents assembly); `LearnedPilotSO`, `RewardWeightsSO`; the Dogfight Training builder; `Engagement` follows the arena; Air Wing's arena, spectator camera and explosions (W59–W64). `LearnedPilotTests` 8/8 (the seat's input log replays its flight to the same Fix; the actions' ranges; the senses see a jet ahead only after the ace's 17-tick delay and never the one behind; a kill pays the shooter 1 + 0.05 a round hit plus its pointed seconds, its squadmate 0.25, the victim −1 − 0.05 a round and its squadmate −0.25, once; a crash, a collision and the box cost 1 each; pointed cone time pays only pointed; a respawned jet rejoins its side). Smokes in Dogfight Training: (1) learned v learned on the stand-in, 120 s: ten decisions a second, collisions 3–7 and 5–8 at the first merges, both back 150 ticks later, squadmates paid −0.25 a loss, no rounds fired (pure pursuit sat 50° off its targets); (2) the stand-in on lead pursuit v line rule pilots, 150 s: blue 2 pointed 34.4 s (+0.69), nobody fired more than 9 rounds, no kills (C44's stalemate); (3) a harness put red 5 at blue 1's pipper at 300 m: shot down in 0.38 s, an explosion, back 150 ticks later, and the 2–8 collision played the second explosion (METHODOLOGY C53); (4) the trainer: `fighter_smoke.yaml`, 30,068 steps in 304 s, mean reward −0.67 at 10,000, −0.20 at 20,000, Elo 1,199 → 1,198, two resets at the self-play side changes, 9 exits from the box and no kills or collisions; `Fighter.onnx` exported (in git-ignored `Training/results`). Found and fixed: the spectator camera never picked up its first jet; the squadron's editor sync left the scene dirty on every open.
- **2026-10-29** — A16 (SIX: missiles for the planes, then 8 v 8): `PilotProfile.MissileRangeM` (Apply Pilot Profiles writes it with its provenance), `AviatorSeat.WillLaunch`, `LearnedCommand.Pickle`, `LearnedActions` with a second branch, `LearnedSenses` version 2, the stand-in's launch, the training scene rebuilt with 16 jets and two action branches. The position-fight tests (`FightTests`) and the gun-fight tests (`GunFightTests`) fly missiles safe, so their criteria keep their meaning; `MissileFightTests` is the missile fight. A brain trained on version 1 (68 senses, one branch) does not fit version 2: train again.

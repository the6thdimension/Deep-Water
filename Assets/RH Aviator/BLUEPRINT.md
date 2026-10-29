# RH Aviator — blueprint

> **Status: LOCKED BLUEPRINT (W36–W51 and A1–A6, SIX 2026-10-26; A7–A8, SIX 2026-10-27; A9 declined, A10–A11, SIX 2026-10-27); Phase 3 complete (3a–3e); slice 3f a go (A11).** The decisions were made in Air Wing's Phase 3 interview and live in Air Wing's register (`Assets/Air Wing/BLUEPRINT.md` §5, W36–W51). This document carries them for the AI side. Progress is in `ROADMAP.md`.
> **This document's one job:** say what RH Aviator is and why, how its AI pilot is built, and which decisions bind it. Work order and progress live in `ROADMAP.md`, not here.

## 1. In plain language

**RH Aviator is the AI pilot.** It sits in an aircraft's seat and flies it the way you do: it issues Air Wing's Pilot Inputs (stick, throttle, pedals, switches) on the fixed tick, through Air Wing's `ISeat` (Air Wing W5, W6). It never moves an aircraft any other way, so everything the player's jet can do, the AI's jet does with the same physics, and a seat can change hands mid-flight (W18).

**What it does first (W17, W49):**
- **Pilot L0:** fly a route (Dogfight's four gates first, W39), join and hold fingertip formation on a leader (W40), go home on an order or at its fuel line (W47), fly the overhead break and land on runway 09/27 (W41).
- **Pilot L1:** fight for position against another jet, scored by the seconds each spends in the other's rear cone (W46). No weapons.

**How it thinks, in three layers:**
1. **The brain** (W36): a small state machine of modes (Navigate, Join, Hold, Return, Break, Land, Fight). Named rules switch modes, and every rule that fires is logged with its tick, aircraft and reason. It decides four times a second (W44).
2. **The plan:** each mode turns the situation into a **flight-path request**: an aim point, or a heading, altitude and speed.
3. **The hands** (W37): one shared layer turns the request into stick and throttle on every tick, the way the mouse instructor does. Tuned once, it serves every mode, and later the player's autopilot.

**What it knows** (W38): at Pilot L0 the true state of the other aircraft; at Pilot L1 only what is inside its field of view, after a reaction delay. Sensors come later.

## 2. Vocabulary

| Word | Meaning |
|---|---|
| **AI seat** | An `ISeat` that RH Aviator implements: the AI pilot of one aircraft |
| **Pilot profile** | A ScriptableObject that sets how a pilot flies: reaction delay, field of view, the G it will pull, its energy sense, its bingo line (W45). Rookie, line and ace are three profiles of one brain |
| **Mode** | What the pilot is doing now: Navigate, Join, Hold, Return, Break, Land, Fight |
| **Rule** | A named condition that switches modes ("gate 4 flown → Return"). Each firing is logged (C19) |
| **Watch** | The brain's decision moment, four times a second on the tick count (W44) |
| **Flight-path request** | What a mode asks the hands for: an aim point, or heading, altitude and speed |
| **Hands** | The shared layer that flies a flight-path request with stick and throttle every tick (W37) |
| **Picture** | What the pilot knows about the other aircraft at a watch (§3.3) |
| **Rear cone** | Within about 30° of a jet's tail and about 1 km behind it; time inside it is the fight's score (W46) |
| **Bingo** | The fuel at which the pilot turns for home; until fuel burns (Air Wing L3) a flight-time limit stands in, labelled as a stand-in (W47) |
| **The break** | The overhead pattern: run in along the runway at 800 ft, a tight level turn, downwind with the gear down, a turn to final, the landing (W41) |

## 3. Architecture

### 3.1 Where things live (W5, W48)

```
Air Wing (DeepWater.AirWing) ← RH Aviator (DeepWater.Aviator) ← its tests
```

- **Air Wing never references RH Aviator.** RH Aviator references Air Wing (and `RH.Shared` for later sensor contracts).
- **In Air Wing:** `ISeat`, `PilotInputs`, the flight core, the gate course, the seat cycle key and the HUD pick list (W42), and **the hands** (A2: next to the mouse instructor, so the player's autopilot can reuse them without Air Wing depending on RH Aviator).
- **In RH Aviator:** the AI seat, the brain (modes, rules, the rule log), the modes' planning, the picture, pilot profiles, the formation and pattern geometry, the fight's scoring, and the scene adapter that places AI seats in Dogfight.
- Layout (Pat1, as Air Wing): `Core/` pure C#; `Modules/` scene adapters; `ScriptableObjects/` pilot profiles and routes; `Examples/`; `Tests/Editor/`.

### 3.2 One tick

1. The flight core asks each seat for its inputs, in aircraft-id order (Air Wing).
2. An AI seat, on a watch tick, runs the brain on the picture: rules fire, the mode may change, and the mode writes a new flight-path request. In formation the request is worked out again on every tick from the picture (A7).
3. On every tick, the hands turn the current request into Pilot Inputs.
4. The flight core logs those inputs and steps the aircraft. A replay replays the inputs; it does not re-run the AI.

### 3.3 The picture (W38, A1)

- At a watch the pilot reads a **snapshot taken at the end of the previous tick**, the same for every pilot (A1). Reading the live list mid-tick would show some aircraft one tick ahead of others, because seats are asked in id order.
- **Pilot L0:** the picture holds every aircraft's true state.
- **Pilot L1:** it holds only aircraft inside the profile's field of view, as they were the profile's reaction delay ago. An aircraft that leaves the field of view is remembered where it was last seen, ageing.

### 3.4 Determinism (Air Wing W7)

- The AI core is pure C# and falls under the same banned-API scan as Air Wing's core: no `Time`, `Random`, `Physics`, `Object.Find*` or wall clock.
- The watch is timed by the tick count: a watch falls on every tick where ⌊tick × 4 / 50⌋ changes, exactly 4 Hz over a second.
- Any randomness (a profile's jitter, if one is ever added) comes from Air Wing's seeded RNG and is logged.
- Two runs with the same start give the same Fix on every tick; the rule log is identical too.

### 3.5 Data (P1)

- **Pilot profiles** (`PilotProfileSO`): reaction delay, field of view, G limit it will pull, energy weighting, bingo (stand-in) and formation slot offsets; values AUTHORED and named as such (Air Wing W8).
- **Routes:** Pilot L0 flies Air Wing's `GateCourseSO` first (W39); a general waypoint route asset follows when a second route is needed.

## 4. The ladder (W49)

| Level | What the pilot does | Knows | Slices |
|---|---|---|---|
| **Pilot L0** | Navigate a route (the gates), join and hold fingertip, go home, fly the break, land | the truth | 3a–3c |
| **Pilot L1** | Fight for position: choose lead, pure or lag pursuit from the geometry and energy, keep its own tail clear; scored by rear-cone time | its field of view, after a reaction delay | 3d |
| Later | Sensors (radar, eyes) via `RH.Shared.ISensorReport`; multi-ship tactics | sensor reports | not planned |

## 5. Decision register

### 5.1 Locked (SIX, 2026-10-26; Air Wing register)

| # | Decision |
|---|---|
| W36 | **The brain:** modes and named rules, each rule logged when it fires |
| W37 | **The hands:** a shared flight-path layer flies every mode's request with stick and throttle, like the mouse instructor |
| W38 | **Perception:** the truth at L0; at L1 a field of view and a reaction delay; sensors later |
| W39 | **First flight:** Dogfight's gate course, then joining on the player's wing |
| W40 | **Formation:** fingertip first (about three wingspans out, slightly behind); trail and line abreast later |
| W41 | **Return and landing:** the overhead break. *SIX's pick; Claude leaned to a straight-in first.* *Mitigation:* the break is built from flight-path segments on the hands, its last segment the straight-in final the landing tests already fly, and is tested a segment at a time |
| W42 | **Seats:** a cycle key and a HUD pick list (Air Wing side); the aircraft the player leaves goes to its AI at once |
| W43 | **How many:** up to four aircraft in all |
| W44 | **Watch:** decide at 4 Hz on the tick count; the hands work the stick every tick |
| W45 | **Skill:** pilot profiles in a ScriptableObject (rookie, line, ace); one brain |
| W46 | **Scoring:** time in the rear cone (about 30°, about 1 km); on the HUD and logged; no weapons, no gun solution |
| W47 | **Going home:** on the player's order or at the profile's bingo; a labelled flight-time stand-in until fuel burns |
| W48 | **Tracking:** this blueprint and its roadmap; Air Wing's Phase 3 points here |
| W49 | **Levels:** Pilot L0 and Pilot L1 |
| W50 | **Testing:** headless flights in EditMode against limits fixed before tuning, then one Dogfight smoke per slice |
| W51 | **Slice 3a:** the AI seat, the hands, the mode machine and the gates |

### 5.2 Locked on Claude's proposal (SIX confirmed A1–A6 as written, 2026-10-26)

| # | Decision | Why | Status |
|---|---|---|---|
| A1 | **The picture is the previous tick's snapshot**, the same for every pilot | Seats are asked in id order; a live read is biased by one tick between aircraft | Locked |
| A2 | **The hands live in Air Wing** (`Core/Seats`, beside `MouseInstructor`) | W37 says the player's autopilot can reuse them; Air Wing must not reference RH Aviator | Locked. *Refined in slice 3a:* not built on `Steer`, which has no bank limit (Air Wing AP25: it banks past 90° toward a low aim); the hands use a bounded law instead (heading error → bank, flight-path error → G, both capped by the pilot profile) |
| A3 | **The player's "go home" order** reaches an AI pilot through RH Aviator's own scene component and input action, not through Air Wing | Keeps Air Wing ignorant of AI orders (W5); the pick list (W42) shows the result | Locked |
| A4 | **An AI cost budget:** ≤ 10 µs per AI pilot per tick in a player build, on top of the flight core, measured with Air Wing's benchmark harness | Air Wing W21's habit; four aircraft must stay well inside a frame | Locked |
| A5 | **The old L0–L5 plan is superseded,** its unchecked items struck through with reasons in `ROADMAP.md`: intercept, BVR, countermeasures, strike and tasking fall outside W17 and the safe limits | W49 replaces the ladder; the roadmap rule keeps the record | Locked |
| A6 | **The legacy flight code is mined first** for pursuit and throttle heuristics, read-only, into `_notes/legacy-mining.md` | The old Phase 0 item; cheap, and it may save tuning | Locked |

### 5.3 Locked on Claude's proposal in slice 3b (SIX confirmed A7–A8 as written, 2026-10-27)

| # | Decision | Why | Status |
|---|---|---|---|
| A7 | **Formation is flown off the slot on every tick**; the 4 Hz watch still decides the mode (Join, Fingertip) and logs it | At 180 m/s the slot moves 45 m between watches and is held to ±5 m. W44 already gives the stick every tick; keeping the slot is the hands' work. The request also carries the leader's bank, roll rate and flight-path rates as feed-forward (AP32), and formation flies its own hands with tighter height and path gains (`PilotProfile.FormationHands`) | Locked |
| A8 | **The seat roster lives in Air Wing** (`Core/Seats/SeatRoster`): one player seat, and each aircraft's *own* seat, which RH Aviator hands over as a plain `ISeat`. Without one the aircraft holds what it was last given (`HoldSeat`). An optional `ISeatStatus` lets a seat say what it is doing for the pick list | W42 puts the cycle key and the pick list in Air Wing; W5 keeps Air Wing ignorant of AI; W18 says the AI flies every empty seat | Locked |

### 5.4 Settled by SIX after slice 3d (2026-10-27)

| # | Decision | Why | Status |
|---|---|---|---|
| A9 | **To score, an attacker must also point at its target** within the cone's angle, as well as sit within 30° of its tail inside 1 km (W46) | By position alone, two jets just past a head-on pass each scored 2.7 s in the other's cone while flying apart; with it, the pass scores nothing and the ace's lead in 20 merges is real | ~~Proposed~~ **Declined by SIX 2026-10-27: score by position alone, as W46 reads.** The pointing check is removed from `RearCone.Inside`; the known cost (a head-on pass scores a few seconds each way) is accepted |
| A10 | **Red's chase stays capped** at its fighting speed plus 150 kt; it does not go to full power after a target that runs | SIX 2026-10-27: running away stays a valid escape. A hands-off player past Mach 1 is not caught | Locked |
| A12 | **Everyone fires** (SIX 2026-10-27; Gunnery K6): trigger discipline from the profile (`GunRangeM`, `TriggerMils`: rookie 1,400 m and 30 mils, line 900 and 12, ace 800 and 6), judged at 10 Hz on what the pilot sees, with the same lead sight the player's pipper uses. Inside 1.5× its gun range the pilot **flies the pipper, not the flight path**: it asks the hands for the flight-path angle that puts the bore on the lead point, and moves its aim sideways by the miss. Kills score first, rear-cone seconds beside; a jet shot down leaves the fight | Built 2026-10-27. Through the height loop alone the aim porpoised (misses 4–187 m); the asked flight-path angle settles it under 1 m | Locked |
| A11 | **Build slice 3f, the fight in the vertical** (energy states, nose-high and nose-low turns, the ace spending its G) | SIX 2026-10-27. Equal pilots stalemate in a level fight (C44). Spike first, with pass criteria fixed before building: line v line and ace v line must each produce a winner in some of 20 merges **by pointed cone time** (the A9 measure, kept as a diagnostic in the spike, not the score), without fights sinking to the floor (the yo-yo failure). *Why pointed:* once A9 was removed, the survey's line v line went from 0 s to 6.1 / 5.6 s a fight, a "winner" in all 20 merges, nearly all of it head-on-pass time; the position score alone would pass 3f with nothing built | Locked; not built |

### 5.5 The learned pilot, built on SIX's training picks (2026-10-29; TRAINING-CONCEPT M21–M28)

SIX: "In the dogfight scene I want you to add 1 red squadron and one blue squadron. Give the planes a machine learning brain and give them the ability to fight each other to train." Eight questions, all answered with the recommended option (M21–M28).

| # | Decision | Why | Status |
|---|---|---|---|
| A13 | **The learned pilot is a seat** (`Core/Learning/LearnedSeat`, `SeatKind.Learned`): a policy outside it decides ten times a second; the seat carries the last command to the hands every tick and keeps the eyes open. **What it asks for** (M1, M23): turn ±180° from its track, climb ±1,500 m, 150–600 kt, a G cap from 2 G to the pilot's most, and the trigger (`LearnedActions`). **What it sees** (M7, M17, M23): 68 numbers through `Perception` with the **ace's eyes and hands** (own state; the 3 nearest enemies and 2 nearest friends as last seen; the pipper on the nearest enemy in view): `LearnedSenses`, layout version 1. Never the truth | W6: it flies only through Pilot Inputs (a test replays its input log to the same Fix). The ace's limits, so a learned pilot wins by judgement, not by a better airframe or better eyes | Built 2026-10-29 |
| A14 | **The referee pays** (`RewardLedger`, M24, weights in `RewardWeightsSO`): kill +1; shot down, crash, collision, friendly kill and leaving the arena −1 each; +0.05 a round landed, −0.05 a round taken or put into a friend; +0.02 a second pointed in an enemy's rear cone (A11's measure); a quarter of each squadmate's kill or loss to every flying squadmate. It judges on the truth; the pilot sees only through its eyes. **The fight follows the arena:** a jet that collides or leaves the box leaves the fight as a kill's victim does, and a respawned jet rejoins on its side (`Engagement.Score`, `SideEver`) | Outcomes first, lightly shaped (M5); the cone pays only when pointed, so the position-only score (A9) can't be farmed | Built 2026-10-29 |
| A15 | **Two training squadrons in `Dogfight Training.unity`** (M21, M22, M27), built by Deep Water → RH Aviator → Build Dogfight Training Scene: blue 1–4 at (0, 1,829, 2,000) heading 000, red 5–8 at (0, 1,829, 22,000) heading 180, line abreast 300 m, 400 kt, on Air Wing's arena (W59–W61) and spectator camera (W62). **One shared brain "Fighter"**, PPO with self-play (blue team 0, red team 1). **Each squadron has a pilot setting** (learned, rookie, line, ace; `TrainingSquadron`): every jet carries both a `FighterAgent` and an `AviatorPilot`, and the setting enables one, so M4's curriculum against the rule pilots is a setting. `FighterTraining` steps ML-Agents' Academy by hand from the tick: pay, end the episode of a jet that went down, cut a life at 180 s, ask flying jets for a decision every 5 ticks, step | SIX asked for squadrons that fight each other now; M10's 1 v 1 first and M4's curriculum first are set aside for this push and kept one setting away (a reversal of order, logged in M21, M22) | Built 2026-10-29 |
| A16 | **Everyone fires missiles** (Air Wing W65; SIX 2026-10-29). Rule pilots launch with the seeker locked on the target they are after, inside their `MissileRangeM` (rookie 5 km: as soon as it locks; line 3.5 km; ace 2.5 km), and not while one of their own is still flying at it. The learned pilot gets a second choice, the pickle, and senses **version 2** (88 numbers: version 1's 68 unchanged, then its missiles left, its lock, and the 2 nearest missiles it sees, aimed at it or not; missiles are seen in its field of view and range, without the reaction delay). The scripted stand-in launches as the ace would. A missile kill pays the kill; a burst that does not kill pays a hit. **The training squadrons are 8 v 8** (SIX 2026-10-29; M21 was 4 v 4) | SIX: "give the planes hard points and a missile armament … change the scene from 4v4 to 8v8" | Built 2026-10-29 |

## 6. Standing constraints

1. **An aircraft changes only through Pilot Inputs from its seat** (Air Wing W6). The AI never writes an aircraft's state.
2. **The AI core is pure and deterministic** (§3.4), under a banned-API scan.
3. **Tuning lives in ScriptableObjects** (P1); AUTHORED values are flagged.
4. **Vendor code is read-only** (P5); legacy code is read, never edited (A6).
5. **Safe limits** (SIX, 2026-10-24; Air Wing §6.8 and General Quarters §6.8 apply in full):
   - ~~**no weapons, no gun solution, no targeting:** the fight is scored by position only (W46)~~ *SIX armed the jets 2026-10-27 (Air Wing W55–W57, A12): a game gun and a game pilot's trigger; still no real fire-control code and no real targeting;*
   - **game AI only:** no autopilot, guidance or control code meant to run on real hardware, drones or model aircraft;
   - opponents stay generic; public data only.
   - *How to apply:* check every slice against this list before it is built.
6. **The 50 Hz tick is not changed;** the brain runs on watches inside it (W44).

## 7. What is deliberately not here

| Not in this blueprint | Why / revisit when |
|---|---|
| Weapons, gun snapshots, missiles, BVR, countermeasures, RWR (the old L2–L3) | Safe limits; only if SIX asks, and then abstract |
| Strike packages, SEAD, tasking (the old L5) | Outside W17; not asked for |
| Multi-ship tactics beyond a wingman and a pair | After Pilot L1; W43 caps the count at four |
| Sensors (radar, IRST, datalink) | W38: later, through `RH.Shared.ISensorReport` |
| The helicopter's AI | With Air Wing's helicopter stream (W20) |
| Carrier approaches and traps | With carrier ops (Air Wing W16, parked until this AI stream is done) |

## 8. Risks

| Risk | Mitigation |
|---|---|
| **The overhead break is hard to tune** (W41) | Built from segments on the shared hands; each segment has its own headless test; the straight-in final is its last segment and already proven |
| **The mouse instructor's pursuit can fly into the ground** (Air Wing AP25) | The hands add altitude and sink-rate guards for the AI only; the player's instructor stays as SIX chose (Air Wing Q5) |
| **An AI that cheats feels wrong** | Pilot L1 sees only its field of view, late (W38); its profile caps the G it pulls (W45) |
| **Formation flying amplifies small errors** | Hold is judged by a set error box in the headless test (W50); the leader's state comes from the one-tick-old picture (A1), so the error is consistent. *Slice 3b:* the box is met with 0.8 m to spare on height, and the formation hands' path gain sits 1.6× inside where the hold rings (C42). A heavier or slower jet, or another fidelity level, needs the sweep run again |
| **One Editor shared with other sessions** | Smokes run only when no other session is driving the Editor (Air Wing's habit) |

## 9. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| ~~Q1~~ | ~~Confirm A1–A6 as written, or change any?~~ **Answered 2026-10-26 by SIX: confirmed as written.** | — |
| ~~Q2~~ | ~~Confirm A7 (formation flown every tick, with feed-forward) and A8 (the seat roster in Air Wing), or change either?~~ **Answered 2026-10-27 by SIX: confirmed as written.** | — |
| ~~Q3~~ | ~~Confirm A9, or score by position alone as W46 reads?~~ **Answered 2026-10-27 by SIX: position alone (A9 declined).** | — |
| Q4 | Air Wing W64 gives a learned pilot its own seat kind (`SeatKind.Learned`) instead of `Ai`. Keep it? | 1 min |

## Change log
- **2026-10-26** — Written from Air Wing's Phase 3 interview (W36–W51, SIX). A1–A6 proposed. Nothing built.
- **2026-10-26** — SIX confirmed A1–A6 and said go on slice 3a. A2 refined while building: the hands use a bounded law, not the instructor's `Steer` (AP25).
- **2026-10-27** — Slice 3b built at SIX's go. A7 (formation flown every tick) and A8 (the seat roster in Air Wing) proposed as Q2. A2 extended: the hands' request may carry feed-forward (bank, roll rate, flight-path angle and rate), zero for a route.
- **2026-10-27** — SIX confirmed A7 and A8 as written (Q2 closed).
- **2026-10-27** — Slice 3d built. A9 proposed (Q3): an attacker must point at its target to score.
- **2026-10-27** — Phase 3 complete. Slice 3f (the fight in the vertical, C44) proposed in the ROADMAP; nothing built.
- **2026-10-27** — SIX answered after the overnight run: A9 declined (position alone; the check removed), A10 red's chase stays capped, A11 slice 3f is a go (spike first). Q3 closed.
- **2026-10-27** — A12: everyone fires (trigger discipline, gun tracking), kills first. The position-fight tests fly with guns safe (`TriggerMils` 0); the armed fight is `GunFightTests`.
- **2026-10-29** — The learned pilot built on SIX's training picks (TRAINING-CONCEPT M21–M28): A13 (the learned seat, its actions and senses), A14 (the referee's reward ledger; the fight follows the arena), A15 (two training squadrons, a pilot setting each, the training director). Q4 added (W64).
- **2026-10-29** — A16: missiles for every pilot (the rule pilots' missile discipline, the learned pilot's pickle and senses version 2), and the training squadrons at 8 v 8.

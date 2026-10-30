> **THEORETICAL; SLICES 1–7 BUILT 2026-10-29/30, the rest not built.** Written 2026-10-29 from SIX's 16 answers in four rounds (J1–J16). SIX:
> "can you give my jets iff like dcs world. and the two radar tracking modes. I want the jets to learn when they are being
> track and have that threat awareness. i want them to have search modes. I want them to be vision limited when something is
> too far to see. I want 2 of the planes missiles to be radar guided. i want planes to also have counter measures".

**This document's one job:** record what giving Deep Water's jets radar, IFF, a radar warning receiver, a radar-guided missile,
countermeasures and limited sight means, and every decision SIX has made about it, so the build can follow the slices without
re-asking. It is not the roadmap (Air Wing's and RH Aviator's `ROADMAP.md`) and it is not a spec.

---

## 1. The idea in plain language

Today a jet knows about another jet only by seeing it: inside its pilot's field of view and a fixed visual range (the ace's is
10 km), late by the reaction delay, remembered for a while (`Perception`). It carries a gun and four heat-seeking game missiles
(W65), whose seeker locks whatever aircraft is nearest the nose, friend or foe.

After this work a jet also has:

- **A radar** in its nose that searches a box ahead, finds contacts by chance (more likely when they are close and big, less
  likely when they hide low over the sea), and keeps **tracks** of them.
- **IFF:** a button that asks every contact in the radar's beam "are you a friend?". Friends answer (usually); the track then
  reads *friend* or stays *unknown*. IFF never says *hostile*.
- **A radar warning receiver:** it hears other jets' radars and shows where each one is (bearing, no range) and what it is
  doing: searching, tracking, or guiding a missile.
- **Two radar missiles** in place of two heat-seekers: steered by the shooter's track at first, then by their own radar near
  the target.
- **Chaff and flares:** a few of each, dropped when the pilot chooses; each gives a set chance to break a radar missile's or a
  heat-seeker's lock.
- **Harder spotting:** seeing another jet becomes a chance that falls with distance and how big it looks, instead of a hard edge.

The rule pilots (rookie, line, ace) and the scripted stand-in use all of it with simple rules. The player gets it last, with a
radar display. A new learned brain first copies the ace, then trains against itself.

Everything stays a **game**: generic jets, invented numbers sized to a 40 km arena, nothing from a real system (Air Wing
BLUEPRINT standing constraint 8).

## 2. Decision register

### Round 1: the radar and the threat

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| J1 | How detailed the jets' radar is | **Game-level radar on the radar suite's maths** (`RHRadarSuite.Core`: radar equation, clutter, measurement errors, track files), inside the pure flight core: each sweep a seeded chance to detect, falling with range and the target's size; contacts become tracks with a little error. Testable without Unity, repeatable | Same | Locked |
| J2 | The two tracking modes | **Track-while-scan** (keeps sweeping, follows several contacts; targets only hear "search") **and single-target track** (the beam on one contact, precise; the target hears "track"; a radar missile can launch) | Same | Locked |
| J3 | What a jet senses about being tracked | **A radar warning receiver:** for each radar painting it, the bearing and the state (search, track, guiding a missile); no range, no identity. The same for player, AI and learned pilots | Same | Locked |
| J4 | Countermeasures | **Chaff and flares**, limited: chaff against radar missiles, flares against heat-seekers; each release gives a set chance to break the lock (invented numbers); the pilot chooses when. *A reversal:* SIX, 2026-10-27, had put countermeasures out of this push (TRAINING-CONCEPT §7) | Same | Locked (reversal of 2026-10-27) |

### Round 2: identity, search, the radar missile, sight

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| J5 | IFF | **Interrogate on demand:** a press asks every contact in the radar's beam; friends answer, with a small set chance of no answer; the track keeps *friend* or *unknown*; IFF never declares *hostile* | Same | Locked |
| J6 | Search modes | **Two: range-while-search** (a wide sweep with range) **and a close-in dogfight mode** (a narrow, tall beam ahead that locks the first contact it finds) | Same | Locked |
| J7 | The radar missile | **Active:** steered by the shooter's track at first; its own radar takes over near the target, after which the shooter may turn away; the target's warning receiver shows "missile" from then | Same | Locked |
| J8 | Vision limits | **Size-based spotting:** keep `Perception` (field of view, delay, memory), but seeing becomes a seeded chance that falls with distance and the target's apparent size (nose-on is harder), not a hard 10 km edge | Same | Locked |

### Round 3: who uses it, and how the brain learns

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| J9 | Rule pilots and the stand-in | **They use it, with simple rules:** search, lock, interrogate before firing at an unknown, chaff or flares on a missile warning; skill sets how well (the ace always interrogates, the rookie sometimes forgets) | Same | Locked |
| J10 | The player | **Yes, in its own later slice:** a radar display on the HUD, keys and pad buttons for the mode, lock, IFF, chaff and flares | Same | Locked |
| J11 | How the new brain learns | **Copy the rule pilots first, then self-play:** ML-Agents' behavioural cloning on recorded fights (the trainer already has the setting: `behavioral_cloning`), blended into PPO and fading. The 1.92-million-step brain is kept as a saved brain | A fresh brain on the full kit | Locked (not the leaning) |
| J12 | The order of work | **The jet's systems first, the pilots last** (§5); each slice verified alone | Same | Locked |

### Round 4: reward, examples, arena, sea

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| J13 | The reward | **No new shaping:** kills and losses pay as now; being locked or shot at costs nothing by itself | Same | Locked |
| J14 | Whom the brain copies | **The ace only**, ace against ace on both sides, recorded headless | Same | Locked |
| J15 | The arena | **Keep the 40 km box**, with invented ranges sized to it: radar search about 40 km, the radar missile about 25 km, the heat-seeker as now | Same | Locked |
| J16 | The sea and radar | **Sea clutter:** a jet low over the sea is harder to detect when the radar looks down at it (the radar suite's clutter model) | Same | Locked |

### Proposed by Claude while writing

| # | Proposal | Why | Status |
|---|---|---|---|
| J17 | **Where it lives:** the jet's systems (radar, IFF, warning receiver, the radar missile, chaff and flares, spotting chance) in Air Wing's core, beside the guns and the heat-seeker; how pilots use them in RH Aviator (rule pilots, the learned seat). Air Wing's core references `RHRadarSuite.Core` for the maths only | A radar is part of the jet, as the gun is; a pilot's judgement is RH Aviator's. Keeps the flight core side-agnostic: friend or foe is a transponder's code, not RH Aviator's sides | Proposed |
| J18 | **Loadout:** 2 radar missiles and 2 heat-seekers on the four hardpoints | SIX: "2 of the planes missiles" | Proposed (confirm) |
| J19 | **Every new chance is a seeded draw** (detection, IFF reply, spotting, countermeasures), like the missile's kill draw | Rule-pilot fights keep exact replay (TRAINING-CONCEPT constraint 5) | Proposed |
| J20 | **Senses version 3** for the learned pilot: radar tracks with their IFF state, the warning receiver's strongest emitters, its own radar mode and lock, chaff and flares left; actions for radar mode, lock, IFF, chaff, flare and which missile. A version-2 brain does not fit it (`BrainLibrary.Fits` gives it the stand-in) | The observation contract (`LearnedSenses.Version`) | Proposed |

## 3. Standing constraints

1. **Safe limits apply in full** (Air Wing BLUEPRINT constraint 8): generic archetypes, invented numbers, public concepts only;
   no real system's figures, no real radar or IFF codes, no tuning to defeat a real system. *Why:* SIX, 2026-10-24, 2026-10-27.
2. **The radar missile is an Air Wing game missile,** like W65, not Guided Fury. *Why:* SIX's rule, 2026-10-27: Guided Fury
   missile work goes to Codex or SIX; SIX asked Claude directly for Air Wing game missiles (2026-10-29).
3. **The brain sees only what a pilot could see:** the radar's tracks, the warning receiver and its eyes, never the truth
   (TRAINING-CONCEPT constraint 2).
4. **Rule-pilot fights stay exactly replayable** (J19).
5. **The heat-seeker keeps no IFF of its own.** Identity is the pilot's job (M35 stands: the learned pilot learns it).

## 4. What is deliberately not here

| Not doing | Why, and the revisit trigger |
|---|---|
| A radar jammer | Not picked (J4); revisit if chaff and flares make radar missiles useless or useless to counter |
| Velocity search | Not picked (J6) |
| Weather blocking sight | Needs the METOC work (Environment maps E1–E25) |
| A bigger arena for long-range fights | Not picked (J15); revisit if radar fights end before anyone closes |
| Datalinks between jets | Not asked for; revisit after the player slice |
| Shaping rewards for threat awareness | Not picked (J13); revisit if the copied habits fade in self-play |

## 5. Slices (J12; proposed order, each verified alone)

| # | Slice | Done when |
|---|---|---|
| 1 | **Radar, search modes and tracks** (J1, J2, J6, J16) in Air Wing's core | EditMode tests: detection falls with range and with low flight over the sea; track-while-scan holds several tracks, single-target track one; the dogfight mode locks the first contact ahead; same seed, same tracks. **Built 2026-10-29 (Air Wing W66): `RadarTests` 11/11** |
| 2 | **IFF and the warning receiver** (J3, J5) | Tests: a friend usually answers and its track reads *friend*; an enemy stays *unknown*; a jet painted in search, track and missile guidance hears each, with the right bearing **Built 2026-10-29 (Air Wing W67): `IffAndWarningTests` 8/8** |
| 3 | **The radar missile** (J7, J18) | Tests: it flies on the shooter's track, goes active near the target, the target hears "missile"; a lost track before going active loses it **Built 2026-10-29 (Air Wing W68): `RadarMissileTests` 7/7** |
| 4 | **Chaff and flares** (J4) | Tests: each release breaks the right missile's lock at its set chance over many seeded draws; counts run out **Built 2026-10-29 (Air Wing W69): `CountermeasureTests` 6/6** |
| 5 | **Spotting by size** (J8) | Tests: the chance falls with distance and nose-on aspect; the rule pilots' fight tests still pass **Built 2026-10-29 (RH Aviator A20): `SpottingTests` 4/4; the rule pilots' fight tests pass** |
| 6 | **The rule pilots and the stand-in use it** (J9) | Headless fights: the ace interrogates before firing at an unknown, drops chaff or flares on a warning; friendly radar-missile launches by rule pilots are zero **Built 2026-10-29 for the rule pilots (RH Aviator A21): `SensorFightTests` 3/3; the stand-in's part moves to slice 7, since it flies through the learned actions and those gain the radar, IFF, chaff and flares only with J20** |
| 7 | **The new brain** (J11, J13, J14, J20): senses version 3, recorded ace fights, behavioural cloning then self-play | A training session runs end to end; the copied brain fires at enemies more than at friends in the who-shot-whom split (C58) **Built 2026-10-30 (RH Aviator A22): senses version 3 (140), six action branches, the ace riding along as a teacher, 16 demonstrations recorded headless, `fighter_bc.yaml` (cloning fading into self-play) running as `sensors-bc`; the 1.92M brain kept and still flies (version 2 shape); `SensorBrainTests` 4/4. Early in the session (60,000 steps) the learning jets launched 353 missiles at enemies and 167 at friends (the kept brain: 30 and 138)** |
| 8 | **The player** (J10): radar display, keys and pad | SIX flies it in the Editor |

## 6. Risks

| Risk | Danger | Mitigation |
|---|---|---|
| Cost per tick | 16 radars sweeping and 16 warning receivers every tick | Sweep on a slower clock than the 50 Hz tick; measure against Air Wing's budget (W21) |
| Too many senses | A bigger observation vector learns more slowly | Few slots, nearest and strongest first; copying the ace first (J11) |
| Countermeasures too strong or weak | Missiles never or always hit | Invented numbers in SOs, tuned by the who-shot-whom split and kill rates |
| Cloning sloppy habits | The ace's rules have their own blind spots | Self-play after cloning; the held-out gate still decides (M11) |

## 7. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| 1 | J18: 2 radar missiles and 2 heat-seekers? | 1 min |
| 2 | How many chaff and flares a jet carries (my lean: 30 each, refilled on respawn)? | 1 min |

## 8. Glossary

- **Track:** the radar's running record of one contact (position, speed, heading, and how sure it is).
- **Track-while-scan:** the radar keeps sweeping while following several tracks; the targets can't tell they are tracked.
- **Single-target track:** the radar's beam stays on one contact for precise data; the target's warning receiver knows.
- **Range-while-search:** a wide sweep that shows each contact's range.
- **IFF (identification friend or foe):** a coded radio challenge that friendly aircraft answer automatically.
- **Radar warning receiver:** a receiver that hears other radars painting the jet and shows their bearing and state.
- **Active radar missile:** a missile with its own small radar, switched on near the target.
- **Chaff:** a cloud of strips that gives a radar a false target. **Flare:** a hot decoy for a heat-seeker.
- **Clutter:** radar echoes from the sea or ground that hide a real target.
- **Behavioural cloning:** a brain learning by copying recorded actions before it learns by trial.

## Change log
- **2026-10-30** — Slice 7 built (RH Aviator A22). J11: senses version 3 add 52 numbers after version 2's 88 (own sensors 8; the 4 nearest firm tracks × 8 with IFF answer, challenges, position, range, lock; the warning receiver's 3 strongest × 4), so a version 2 brain is still fed its first 88. J20: four more action branches (radar: leave, search, unlock, lock the track in slot k; IFF; decoys none, chaff, flares, both; the radar pickle); a radar command is given once, on the tick after the decision. J13: an ace `AviatorSeat` rides along in each learned seat and judges every tick; its flight path and inputs become the agent's actions (`LearnedActions.FromRequest`, `FromInputs`), its momentary radar command and radar pickle kept until the next decision (missing that first, the copied ace launched nothing). The headless player records it with `-recordDemos 6000`: 16 files, 96,000 decisions, 34 s of wall clock at ×20; the teacher launched 102 missiles, none at a friend. J14: `Training/config/fighter_bc.yaml`, behavioral cloning (strength 0.5, fading over 1 million steps) under self-play PPO, run `sensors-bc` on four headless players. The 1.92M-step brain is kept as `Policies/Kept/SelfPlay-v2-1920k` and still flies: a jet shapes its Behavior Parameters for the brain it carries. The stand-in now works the kit through the rule pilots' `SensorHabits`. Who shot whom, logged by the training director every 3,000 ticks: at 60,000 steps the learning jets' missiles went 353 at enemies, 167 at friends.
- **2026-10-29** — Slice 6 built for the rule pilots (RH Aviator A21): `SensorHabits` (search; pick the nearest firm track ahead; hostile by sight or by three unanswered challenges in the beam, and only when the jet carries a code; lock, one radar missile at a time, unlock once it is active; chaff on the warning receiver's "missile", flares on a heat-seeker seen coming) and `JetRadar.ChallengesOf`. A rookie that forgets to ask can shoot only what it has seen, so even its forgetting never aims a radar missile at a friend. Measured: two aces v two line pilots from 30 km: 4 radar missiles, all after asking (first at 11.7 km); ace v ace from 18 km: 5 warnings, all answered, 15 chaff and 10 flares, 5 missiles decoyed; three seeds of a mixed 4 v 4 from 30 km for 150 s: 42 radar missiles, 0 at a friend, 40 of 50 warnings answered, 15 contacts forgotten. The stand-in's use moves to slice 7 (J20's actions).
- **2026-10-29** — Slice 5 built (RH Aviator A20): spotting by size in `Perception` (`PilotProfile.SpotRatePerS`, `SpotHalfRangeM`, `SpotNoseSize`, `SpotTailSize`; `FlightCore.Seed` so each pilot's eyes draw their own stream). Measured, line pilot: at 4 km 0.50 spots a second from the beam, 0.27 from behind, 0.20 nose-on; over 200 seeds the mean wait was 2.45 s at 3 km beam-on, 4.23 s at 6 km beam-on and 4.23 s at 3 km nose-on. A jet the pilot knows (seen, not yet forgotten) is seen again the moment it is in view.
- **2026-10-29** — Slice 4 built (Air Wing W69): chaff and flares (`CountermeasureProfile`, `PilotSwitches.Chaff` and `Flare`, `MissileEventKind.Decoyed`). Open question 2 at the leaning: 30 of each, refilled on respawn. Measured: over 4,000 seeded draws chaff broke 25.0 % of radar missiles and flares 24.6 % of heat-seekers (set 25 %); a target dropping flares broke the heat-seeker in all six seeds at 3 km, which may make flares too strong once rule pilots use them (slice 6 will show).
- **2026-10-29** — Slice 3 built (Air Wing W68): the active radar game missile (`MissileSeeker.ActiveRadar`, `MissileMount.RadarMissile`, `PilotInputs.RadarPickle`, `JetRadar.TryLockEstimate`). J18 at the leaning: two heat-seekers outboard, two radar missiles inboard. Measured: launched at 18 km, active at 16.3 s, burst at 31.6 s, a kill; the target hears only the shooter's track before active, "missile" after.
- **2026-10-29** — Slice 2 built (Air Wing W67): IFF (`IffProfile`, `TrackIdentity`, `Aircraft.IffCode`, `FlightCore.SetIffCode`, `PilotSwitches.Interrogate`) and the warning receiver (`WarningReceiver`, `RadarPaint`, `PaintKind`, `WarningContact`). RH Aviator gives each side its own code where jets join a fight. Measured: 286 answers to 300 challenges (95.3 %, the authored 0.95); a jet 20 km ahead hears the search dead ahead at 0°/−3° (the −3° is its angle of attack). SIX, mid-slice: "keep rolling through till all the slices are done, then do an overnight training run"; the two open questions are taken at the leaning (J18 2 + 2; 30 chaff and 30 flares).
- **2026-10-29** — Slice 1 built (Air Wing W66): `Core/Radar` (`RadarProfile`, `JetRadarLook`, `JetRadar`), the radar stage in `FlightCore` after the arena, `PilotInputs.Radar` and `RadarTrack`, the F/A-18E's generic radar and signature (`RadarData`; peak power solved to 16,856 W). Measured nose-on Pd per look: 0.995, 0.931, 0.700, 0.344, 0.096 at 10–50 km; at 20 km over a Beaufort 4 sea a jet at 60 m drops from 0.928 to 0.297 (clutter improvement 30 dB, the 30 % lean). The jets' senses are unchanged (version 2): the brain does not see the radar until slice 7.
- **2026-10-29** — Written from SIX's 16 answers (J1–J16; J11 not the leaning; J4 reverses 2026-10-27); J17–J20 proposed. Nothing built.

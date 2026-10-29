# RH Aviator

The AI pilot. It sits in an Air Wing aircraft's seat and flies it through the same Pilot Inputs the player uses. Follows McMahon
Pat1: **SO-Profile + Pure-Core + MB-Adapter**. **Read `BLUEPRINT.md` first** (W36–W51, SIX 2026-10-26); progress is in `ROADMAP.md`.

## Try it (slice 3a)

1. Open `Assets/_SCENES/Dogfight.unity`.
2. **Deep Water → RH Aviator → Add AI Jet to the Open Scene.** An AI F/A-18E appears 80 m right of and behind your jet. The scene is not saved; reload it to take the AI jet out.
3. Press Play. The AI flies the four gates (about 5.5 minutes, with a go-around at a gate it cannot line up for first time). Each rule it fires prints in the Console as `[RH Aviator] tick … · next gate / go around / back on the line / course flown`.

## Try it (slice 3b)

1. Open `Assets/_SCENES/Dogfight.unity`.
2. **Deep Water → RH Aviator → Add AI Wingman to the Open Scene.** An AI F/A-18E appears 600 m behind and 200 m right of your jet, ordered to join your right wing; your own jet gets an AI pilot too, for when you leave it. Unsaved.
3. Press Play. The wingman closes and settles into fingertip in about half a minute. **Tab** (or the Xbox **D-pad right**) moves you into the next jet; the pick list, top right, shows who flies what. The jet you leave goes to its AI at once.

## Try it (slice 3c)

After **Add AI Wingman**, press **H** (or the Xbox **D-pad up**) once the wingman has joined: every AI pilot not in your jet goes home to runway 09/27, flies the overhead break and lands. Its legs print in the Console and show in the pick list (HOME · DOWNWIND …).

## Try it (the 4 v 4)

Open Dogfight, then **Deep Water → RH Aviator → Add 4 v 4 Dogfight to the Open Scene**: three blue wingmen on your wing (left, right, outside right), four red jets 20 km ahead (rookie, line, line, ace) hunting you, a score panel at the top. Press Play and fight; **Tab** moves you between your jets; **H** sends the AI pilots home. Let go of the keys and your jet holds level (LEVEL HOLD on the HUD). No weapons: the score is seconds in the enemy's rear cone.

## Try it (training: two squadrons that learn by fighting each other)

Built 2026-10-29 on SIX's picks (TRAINING-CONCEPT M21–M28; BLUEPRINT A13–A15; Air Wing W59–W63).

**Watch it.** Open `Assets/_SCENES/Dogfight Training.unity` (or rebuild it: **Deep Water → RH Aviator → Build Dogfight Training Scene**) and press Play. Eight a side (8 v 8): blue 1–8 start south at (0, 1,829, 2,000) heading north, red 9–16 at (0, 1,829, 22,000) heading south, 20 km apart. Every jet carries the gun and four game missiles (Air Wing W65): a lock is a target within 30° of the nose, 500 m–5 km, held a second; the box over a target dashes while locking and reads LOCK when locked. A jet that is shot down, collides, hits the sea or leaves the 40 km box goes down with an explosion and comes back at its spawn point 3 s later, protected for 5 s. There is no player seat: the **spectator camera** follows a jet (Tab or ] next, Shift+Tab or [ previous, 1–9 by id; hold the right mouse button to orbit, scroll to zoom) or flies free (F; WASD, Q/E down and up, right mouse to look, Shift fast, scroll for speed). L hides the jets' tags. Without a trainer or a model the jets fly a **scripted stand-in** (lead pursuit at the ace's 6 mils, missiles locked inside 2.5 km): it chases, but it is not a pilot. In Dogfight, you launch a missile with the right mouse button or M, the Xbox right shoulder, or stick button 6 (pad B stays the gear).

**Train it.** The trainer is `mlagents` 1.1.0 in `C:\Users\SIX\venvs\mlagents-1.1.0` (Python 3.10.11, CPU PyTorch). From the project root:

```
C:\Users\SIX\venvs\mlagents-1.1.0\Scripts\mlagents-learn Training/config/fighter_selfplay.yaml --run-id=fighter-001 --results-dir=Training/results
```

then press Play in Dogfight Training when it says it is listening. Watch the curves with `C:\Users\SIX\venvs\mlagents-1.1.0\Scripts\tensorboard --logdir Training/results`. `Training/config/fighter_smoke.yaml` is a few-minute run that proves the loop; A brain trained before 2026-10-29's missiles (senses version 1, one action branch) does not fit now: train again. `fighter_vs_rules.yaml` trains blue against a rule-pilot red (set the Red Spawn's **Training Squadron → Pilot** to Rookie, Line or Ace first: M4's curriculum).

**Use a policy.** Each run exports `Training/results/<run>/Fighter.onnx` (git-ignored). Drag it onto every jet's **Behavior Parameters → Model** and press Play with no trainer running. A policy worth keeping goes into `Assets/RH Aviator/Policies/` with its reward weights, settings and step count (M19).

**Where it lives.** `Core/Learning/` (pure: `LearnedSeat`, `LearnedSenses`, `LearnedActions`, `RewardLedger`), `Modules/Learning/` (ML-Agents: `FighterAgent`, `FighterTraining`, `TrainingSquadron`, its own assembly so the rest of RH Aviator never depends on ML-Agents), `ScriptableObjects/Training/` (`TrainingArena`, `LearnedPilot`, `RewardWeights`, written by **Apply Training Assets** with their provenance), Air Wing's `Core/Arena.cs`, `SpawnPoint`, `AirWingArena`, `SpectatorCamera`, `ExplosionView`. Tests: `LearnedPilotTests` here, `ArenaTests` in Air Wing.

## The fight (slice 3d)

`AviatorSeat.FightIn(id, engagement)` puts a pilot in a fight: an `Engagement` holds the sides and whom red hunts (the player's jet, W52). Pilots see through their eyes (field of view, reaction delay, memory) and are scored by seconds in an enemy's rear cone (W46). No weapons. Rookie, line and ace live in `ScriptableObjects/Pilot_*.asset` (**Apply Pilot Profiles**).

The line pilot's numbers are in `ScriptableObjects/Pilot_Line.asset` (written by **Apply Line Pilot Profile**, all AUTHORED). The AI's cost: **Build Benchmark Player**, then `Builds/AviatorBenchmark/AviatorBenchmark.exe -batchmode -nographics -aviatorBenchOut <file>` (3–4 µs per pilot per tick, budget 10).

**Assembly:** `DeepWater.Aviator` (namespace `DeepWater.Aviator`). Renamed from `RH.Aviator` on 2026-10-25 by SIX's call (Air Wing BLUEPRINT Q2), so it matches the other `DeepWater.*` modules.

**Since 2026-10-25, Air Wing owns the airframe side** (`Assets/Air Wing/BLUEPRINT.md`, W5, W6, W17). RH Aviator stays AI-only. Its AI seats issue Air Wing's `PilotInputs` through `ISeat`, exactly as the player does. That replaces the `IAircraftActuator` contract below. Aircraft weapons (L2–L3 below) stay out unless SIX asks (Air Wing §6, constraint 8).

## Folder layout

```
RH Aviator/
  Core/              — pure C#: the AI seat, the brain (modes, named rules, the rule log), the picture, formation and pattern geometry, fight scoring
  Modules/           — scene adapters: placing AI seats in Dogfight, the go-home order
  ScriptableObjects/ — PilotProfileSO (rookie, line, ace), routes
  Prefabs/           — (none planned: the jet is Air Wing's)
  Examples/          — Dogfight AI: the gates, formation, the break, 1v1
  Tests/Editor/      — colocated tests
```

## The ladder (W49)

| Level | What the pilot does | Knows |
|---|---|---|
| **Pilot L0** | Navigate a route (Dogfight's gates first), join and hold fingertip, go home, fly the overhead break, land | the truth |
| **Pilot L1** | Fight for position, scored by time in the other jet's rear cone. No weapons | its field of view, after a reaction delay |

The old six-tier table (intercept, ACM with guns, BVR, multi-ship, mission AI) is superseded; its items are struck through with reasons in `ROADMAP.md` (BLUEPRINT A5).

## Contracts

- **Output to the aircraft:** Air Wing's `ISeat` and `PilotInputs` (stick, throttle, pedals, switches; Air Wing W5, W6). The AI never writes an aircraft's state.
- **References:** `DeepWater.AirWing` (from slice 3a) and `RH.Shared`. **Air Wing never references RH Aviator.**
- **Later:** sensor input through `RH.Shared.Contracts.ISensorReport` (W38: after Pilot L1).
- ~~`IAircraftActuator`~~ superseded 2026-10-25 by Air Wing's `ISeat` and `PilotInputs`.

## Why this is decoupled from the actual flight model

The flight model (Air Wing's first-party ladder; formerly the AerialArcade F-18E, Flight Sim Lite) is **not** part of
this module. RH Aviator decides *what* to fly (a gate, a formation slot, the break, a fight);
the hands turn that into stick and throttle, and Air Wing's flight controls move the surfaces. The stick means
the same at every Air Wing level, so an aircraft can move up a level without re-authoring the AI.

## Legacy fragments

- `Assets/_Archive/Aircraft_Handlers_legacy/MouseFlight/Scripts/AI_FlightController.cs` (archived 2026-10-25, Air Wing slice 0)
- `Assets/_Archive/FighterJet Scripts_legacy/FlightControllerManager.cs` (archived 2026-10-25, Air Wing slice 0)
- `Assets/JMAC/Agents/{SimpleHomingMissile, SimpleDamageable, WeaponSystem}.cs`

Slice 3a mines these read-only for pursuit, throttle and turn heuristics (BLUEPRINT A6) and leaves them in place.

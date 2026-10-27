# Gunnery — blueprint

> **Status: LOCKED (SIX, 2026-10-27).** Slice 1 (the move of General Quarters' ballistics) and slice 2 (the aircraft
> gun: rounds in flight, the hit test, the lead sight) are built. Progress is in `ROADMAP.md`.
> **This document's one job:** say what Gunnery owns and why, and record every decision behind it.

## 1. In plain language

**The ask.** SIX, 2026-10-27, while asking for the jets to be armed:

> "Perhaps the gun and ballistics works should be maintained and authored by another module."

Gunnery is the one place where guns and their rounds are modelled, for every platform. A ship's 5-inch gun and a
jet's cannon fly their rounds with the same drag, the same air and the same hit tests. The vehicles own the guns'
mounts, their aiming and their damage; Gunnery owns what happens between the muzzle and the hit.

**Module boundaries** (SIX, 2026-10-27):

| Module | Owns |
|---|---|
| General Quarters | Ships: hull, combat system, mounts, fire control, damage control |
| Air Wing | Planes: flight, cockpit, the gun's place on the airframe, hardpoints, the aircraft's hit points |
| RH Aviator | The AI pilots, including when they shoot |
| **Gunnery** | **All gunnery:** round flight, drag, air for ballistics, firing tables, shot groups, hit tests, gun profiles |
| Guided Fury | **All missile behaviour:** seeker, guidance, motor, aero, fuze, profiles |
| RH Shared | Contracts between them, such as the hit report |

## 2. Decision register

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| K1 | What Gunnery owns | **All gunnery, for every platform.** General Quarters' `Core/Ballistics` moves here; GQ keeps its mounts, fire control and orders. One implementation of round flight | Same | Locked |
| K2 | The name | **Gunnery** (`Assets/Gunnery`, `DeepWater.Gunnery`) | Same | Locked |
| K3 | Who owns damage | **The vehicle.** A hit report contract in RH Shared (who hit, where, with what); Gunnery rounds and Guided Fury warheads both deliver one; each vehicle applies it to its own hit points | Same | Locked |
| K4 | The aircraft gun | **Simulated rounds** flown as shot groups, seeded and deterministic, **plus a lead-computing HUD pipper** | Rounds with a fixed gun cross | Locked |
| K5 | What a hit does to a jet | **Abstract hit points;** at zero the jet is killed, leaves the fight and falls; the kill is logged with its shooter | Same | Locked |
| K6 | Who fires | **Everyone:** the player and the AI (trigger discipline from the pilot profile). The fight scores kills first, rear-cone seconds beside | Same | Locked |

## 3. Standing constraints

1. **Gunnery references no vehicle or missile module** (a test, `Gunnery_ReferencesNoVehicleOrMissileModule`).
   *Why:* it serves all of them; a reference back would tie the ships to the planes.
2. **The core is deterministic** (the banned-API scan, copied from General Quarters).
3. **Safe limits** (General Quarters §6.8, Air Wing constraint 8): game gunnery, public data, abstract lethality,
   no real fire-control code, no tuning against a real system.

## Change log
- **2026-10-27** — Written from SIX's answers (K1–K6). Slice 1: General Quarters' ballistics moved here.
- **2026-10-27** — Slice 2: `Guns/` (GunProfile, RoundFlight, RoundsInFlight, LeadSight) and the RH Shared `HitReport`; Air Wing and RH Aviator use them.

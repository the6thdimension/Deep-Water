---
name: gunnery-integration
description: "Put a gun on any Deep Water vehicle (ship, aircraft or ground vehicle) through the shared Gunnery module (Assets/Gunnery, DeepWater.Gunnery). Covers the module lines (Gunnery owns ballistics and round flight; the vehicle owns its mount or rail, its slew limits, when to fire, its hit points and its tick), authoring a GunProfile with labelled public figures, wiring a new consumer step by step (asmdef, profile asset, the guns stage in a FixedUpdate tick, the pipper and tracers), firing tables and fire control by lookup, lead sights, shot groups and dispersion, rounds in flight and the swept hit test, hit reports into abstract hit points, seeded determinism and the Fix, and the tests (GunTests, the banned-API scan, a consumer test template). Use it whenever someone says add a gun, arm the ship or the jets, gun profile, pipper, lead sight, gun cross, firing table, ballistics, rounds, tracers, shot group, dispersion, hits, hit points, everyone fires, trigger discipline or Gunnery, or when a gun jitters, misses what it aims at, or its pipper disagrees with its tracers. Not for missiles: those belong to Guided Fury."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Gunnery integration

**Module lines (SIX, 2026-10-27; Gunnery `BLUEPRINT.md` K1–K6):**

- **Gunnery** owns all ballistics and round flight, for every platform.
- **Each vehicle module** owns its mounts or rails and decides when to fire.
- **The target's module** applies the damage, through RH Shared's `HitReport` (K3).
- **Missiles are not Gunnery's.** They belong to Guided Fury.

**Done twice:**

| Consumer | How it aims and hits | Commits | When to copy it |
|---|---|---|---|
| General Quarters' ship mounts | Fire control by firing-table lookup, with a statistical shot group per burst | `753dc1e`, `d02d226`, `76c2d6a`; the ballistics moved into Gunnery in `118e8829` | Fire control by lookup |
| Air Wing's jets | Flown rounds, with a HUD pipper; RH Aviator pulls the AI's trigger | `3e34e232` | Rounds that fly and hit moving things |

The worked examples, file by file: [references/consumers.md](references/consumers.md). Every profile field and the API:
[references/gun-profile.md](references/gun-profile.md). What each commit taught: [references/history.md](references/history.md).

## 1. When to use it

- A new gun on a ship, an aircraft or a ground vehicle; a new or changed gun profile.
- Lead sight or pipper work; the AI's trigger or gun tracking.
- Firing tables, fire-control solutions, form-factor calibration.
- Shot groups, rounds in flight, tracers, hits, hit points and kills.
- Any change to `Assets/Gunnery/Core` (then run every consumer's tests). For a ship gun's next rung, add `ladder-rung-slice`.

## 2. The seam

| The vehicle provides | Gunnery provides |
|---|---|
| The mount or rail: the muzzle and bore line in the world frame each tick (Air Wing: `Position + Attitude * MuzzleM`, `Attitude * BoreBody`) | Round flight (`RoundFlight`): gravity, ISA air, G7 drag, midpoint steps. These are the firing table's physics, so a shell and a cannon round fly alike |
| Slew and limits: train and elevation rates, arcs, elevation limits, the laying tolerance (General Quarters' `GunMountL1.Slew`) | Lead: `LeadSight.RoundAt` (the pipper), `LeadSight.MissDistance` (the AI's trigger), `FiringTable.TrySolve` (fire control by lookup) |
| The fire command and its rate: trigger or doctrine, rounds per tick with the fraction carried over, rounds left | `RoundsInFlight`: volleys, the swept closest-approach hit test, one draw per round, `HitReport`s out |
| The tick, the stage order, the seeded RNG (an `IUniformSource`) and the Fix | Dispersion and the hit chance (`ShotGroup`), and the `GunProfile` type |
| Targets (id, path over the tick, radius), hit points, and what a hit does | Nothing about targets beyond that sphere. No damage, no mounts, no aiming decisions |

**Determinism.** Same seed and same inputs give the same hits on the same ticks.

- **One fixed tick:** a `MonoBehaviour` adapter calls the core's `Step` from `FixedUpdate`. Gunnery never reads a clock; `dt` is passed in.
- **Seeded draws:** every hit is decided by a draw from the caller's seeded source (`GunProfile.cs:38`). Gunnery has no randomness of its own. Volleys go in firing order, targets in the caller's order.
- **The Fix:** it carries the gun's state. In Air Wing that is rounds, carry and hit points (`FlightFix.cs:84`–`86`); in General Quarters, `GunMountL1.Fingerprint`. Air Wing's Fix does not hash the volleys in flight, so a divergence shows only when it changes a hit.
- **Tick order:** fly the rounds already in flight first, then fire this tick's (`FlightCore.cs:146`–`148`). Firing order then cannot decide who is hit.

**Safe limits** (General Quarters `BLUEPRINT.md:214`–`228`, Air Wing `:209`–`214`, Gunnery constraint 3): game gunnery,
public cited data, abstract lethality (hit points or an AUTHORED kill chance per hit), and generic opponents. No
real fire-control code, no tuning against a real system. The pipper is "a game sight" (`LeadSight.cs:9`). Check every
slice against the list before building.

## 3. Ask SIX first (multiple choice), then record the picks

1. **Which path?** (a) Flown rounds with `RoundsInFlight` and `LeadSight`, as Air Wing. (b) Firing-table lookup with a shot group per burst, as General Quarters L1. (c) Both: look up to aim, fly to hit. This is Gunnery `ROADMAP.md:15`, for General Quarters' CIWS.
2. **What data exists?** Is there a public muzzle velocity, rate and rounds carried? Is there a published range to calibrate the form factor to (C26)? Anything else is AUTHORED.
3. **Lethality:** hit points per round (Air Wing W57) or a kill chance per hit (General Quarters G36). Both are abstract.
4. **Who fires:** a player binding, an AI rule judged on what the AI sees (RH Aviator A12), or doctrine.
5. **What shows:** a pipper, tracers, a mount model following the core's lay.

The vehicle's `BLUEPRINT.md` gets a decision row per pick. A Gunnery-wide change gets a K row in Gunnery's.

## 4. Author the gun profile

`GunProfile`: `Name`, `Round` (`ProjectileData`: `MassKg`, `DiameterM`, `MuzzleVelocityMps`, `FormFactor`),
`RoundsPerMinute`, `RoundsCarried`, `DispersionMils`, `MaxTimeOfFlightS`. Each field's unit, its measured effect in
play and the repo's labelled values: [references/gun-profile.md](references/gun-profile.md).

- **The numbers live in a ScriptableObject,** baked into a `GunProfile` and validated in `Bake`.
- **An editor command writes them, with a provenance paragraph naming each number's source and label** (Air Wing's `Editor/GunData.cs`; General Quarters' `Ddg51CombatData` → `Calibration~/GUNS-SOURCES.md`). Never hand-edit the asset.
- **Labels:** VERIFIED (read on a cited public page), MEASURED (from the running model), DERIVED (computed from those), CALIBRATED (fitted to one named public figure), VALIDATION (compared, not fitted), AUTHORED (a choice: nothing public exists). Air Wing's provenance also writes PUBLIC (read as VERIFIED) and SECONDARY (a secondary source, not pinned for the variant).
- **Calibrate the form factor** to one published range with `FiringTable.CalibrateFormFactor`, and report how far it is from 1.

## 5. Wire a new consumer, step by step

1. **Assemblies.** Add `"DeepWater.Gunnery"` to every asmdef that names a Gunnery type (General Quarters: runtime, Editor and tests; Air Wing: runtime and tests), plus `"RH.Shared"` if you use `RoundsInFlight` or `HitReport`. First-party asmdefs do not auto-reference. Never make Gunnery reference your module: add your asmdef name to `Gunnery_ReferencesNoVehicleOrMissileModule` (`GunneryCoreBannedApiTests.cs:59`).
2. **Vehicle types.** A mount struct holding a `GunProfile`, the muzzle and the bore in the body frame, and its limits. A combat struct for hit points and the hit radius. Air Wing: `GunMount` and `CombatProfile` (`ArmamentProfile.cs:11`, `:29`).
3. **The profile asset and its builder command** (§4).
4. **State.** Rounds left, the rate carry and hit points live in the vehicle's state, start full, and go in the Fix.
5. **The guns stage in the core's tick**, after the vehicles move. In this order:
   1. Build each target's `GunTarget`, from last tick's position to now.
   2. Run `Rounds.Step(tick, dt, targets, rng, hits)`.
   3. Apply each `HitReport` to hit points, skipping the dead.
   4. Fire: for each gun with its trigger held, laid and with rounds left, add rpm / 60 × dt to the carry, fire the whole rounds with `Rounds.Fire`, and keep the fraction. A released trigger zeroes the carry.

   Air Wing's is `FlightCore.Guns` (`FlightCore.cs:150`–`193`); General Quarters' is `GunMountL1.Step`.
6. **The adapter.** `FixedUpdate` calls `core.Step()`, with `DefaultExecutionOrder` before anything that reads the vehicle (Air Wing −50, General Quarters −100). The seed comes from the fidelity sheet or the loadout.
7. **The fire command.** An input action or an AI rule writes a field in the vehicle's inputs (`PilotInputs.Trigger`), so it goes in the input log and replays.
8. **Show it.** Presentation reads the core and never aims on its own.
   - The pipper is `LeadSight.RoundAt`, drawn in `OnGUI`.
   - Tracers come from `Rounds.Live`, drawn in `LateUpdate`.
   - Mount models follow the core's lay (`TryGetLay`) and rebind each run.
9. **Fast Play.** A new runtime asmdef goes on `FirstPartyRuntime` (`StaticStateTests.cs:21`). Any new mutable static is reset at play start, or allow-listed with a reason.

## 6. Test

- **Gunnery's `GunTests` (7)** pin: the vacuum drop (½gt²); the 3-D round against `FiringTable.Fly` at 2 s (asserted within 1 m; it measures identical to 0.1 m); a hit on target and a miss 20 m wide; no self-hits; partial hits with dispersion, identical under one seed; a fired round passing the pipper within one step of flight (25 m); a led burst missing by under 1 m, against over 50 m pointing at a crossing target.
- **`GunneryCoreBannedApiTests` (3):** `Assets/Gunnery/Core` uses no `Time`, `UnityEngine.Random` or `new System.Random`, `Physics.`, `Find*Object*`, `GameObject`/`MonoBehaviour`/`Transform`, `DateTime.Now`/`Environment.TickCount`/`Stopwatch`, or `Parallel`/`Task.Run`/`ThreadPool`. `Vector3`, `Quaternion` and `Mathf` are allowed: "engine-free" here means no scene, no clock and no global RNG, not no `UnityEngine`. It also checks that Gunnery references no vehicle or missile, and that the scanner ignores comments. Your own `Core/` needs the same scan (Air Wing and General Quarters each have `CoreBannedApiTests`).
- **The consumer's own tests:** start from [templates/gun-consumer-tests.cs](templates/gun-consumer-tests.cs). Its eight tests: the profile validates and refuses an empty gun; the rate with the carry; firing only once laid; hits into hit points, a miss taking none; this tick's rounds fly from the next; the same seed gives the same hits tick for tick; the pipper agrees at your tick; a dense firing-table sweep (AP21). Built and run outside the Editor (.NET 8 against Unity's `UnityEngine.CoreModule`, 8/8); not yet run in the Unity Test Runner. Replace its stand-in stage with your core.
- **After any change to `Assets/Gunnery/Core`,** run Gunnery, General Quarters, Air Wing, RH Aviator and Fast Play (256 tests at `3e34e232`). Fix values move when guns do; pin determinism per level, never paste a new golden (C29).

## 7. Traps (each cost real time)

1. **A firing table that does not reach the query loses its bracket** (`76c2d6a`, AP21). Shots flown to only −100 m made `TrySolve` fail on alternate ticks at 14–20 km, and the Mk 45 "jerked": 48 drops and 86 train reversals. Fly to −3,000 m (`FiringTable.cs:36`), bracket against the floor, and sweep every 25 m in a test. When a gun jitters, log its decision per tick before touching the motion.
2. **What you see must be what fired** (`d02d226`). The ship model slewed on its own and did not match the core. Follow `TryGetLay` at twice the rig's rate, and rebind on each run.
3. **Arming changes old results.** The Mk 45 fix moved the L1 saturation Fix (`79981241FC806144`), and General Quarters' golden (`2AF5C040E4AC6B5E`) pins guns L0. RH Aviator's position fights now fly guns safe (`TriggerMils` 0) so their numbers hold.
4. **A form factor far from 1 is a finding, not a fit** (C26): Phalanx's is 3.2484. The 20 mm's 1.0 is AUTHORED and uncalibrated (Gunnery `ROADMAP.md:14`).
5. **No silent fallback.** Guns L1 without data throws (`Guns.cs:260`), and so does `GunProfile.Validate`.
6. **Moving code:** `git mv` keeps GUIDs, the moved files lose the old module's scan, and Fast Play's hand-kept allow-list goes stale (`118e8829`).
7. **AI gun tracking through the height loop porpoised** (misses of 4–187 m). Command the flight-path angle that puts the bore on the lead point instead: under 1 m (`3e34e232`, RH Aviator A12).
8. **A closed form exact in one limit, carried into another on an unmeasured claim.** Until 2026-10-29 the hit model multiplied the centred chance by exp(−d²/2σ²). That is exact only when the target is much smaller than the spread. Its comment said it erred toward hits, but at r/σ = 4 and a pass 2σ off centre it gave 0.135 per round against 0.966 true. It is now the exact disc integral, pinned to 2e-6 by Gunnery's `ARoundsHitChance_*` tests. Don't swap in a cheaper shortcut without checking it against the exact series across r/σ (METHODOLOGY AP36). The numbers and what the fix moved are in `gun-profile.md`.
9. **Rounds hit anyone but the shooter, friends included.** The AI's trigger has no friendly-fire check (Gunnery `README.md:17`, `ROADMAP.md:13`).
10. **Rounds in flight have no ground.** Volleys are dropped only below `FloorM` (−100 m, `RoundsInFlight.cs:52`). A ground vehicle must test terrain itself.
11. **`MaxTimeOfFlightS` caps the pipper.** The F/A-18E's 3 s reaches 2,072 m from rest. Beyond that, `RoundAt` is false and no pipper is drawn, though the HUD ranges targets to 2,500 m (`FlightHud.cs:115`).
12. **Velocity at the muzzle differs between paths.** General Quarters' firing table ignores the ship's velocity (`GUNS-SOURCES.md:13`), while flown rounds add it (`RoundsInFlight.cs:73`). Allow for the gap (up to 2 % of muzzle velocity at 30 kn) if a gun mixes the two.

## 8. Close out

- **Gunnery:** the `README.md` "Used by" line, `ROADMAP.md` (done and next), a `BLUEPRINT.md` K row for anything Gunnery-wide.
- **The vehicle:** a `BLUEPRINT.md` row per pick; the slice in `ROADMAP.md` with measured numbers and what was not verified.
- **METHODOLOGY** for lessons that generalise (`methodology-scribe`); **this skill:** the consumer in `references/consumers.md`, new traps in §7.
- **Commit** with `commit-to-branch`, then level the branches.

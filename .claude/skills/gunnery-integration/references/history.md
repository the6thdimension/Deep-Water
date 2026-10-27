# What each gun commit taught

Read with `git show <hash>`. Each entry gives the problem, the change, the lesson, and where the lesson is pinned.

## `753dc1e`: General Quarters guns L1 (2026-10-24; G34–G41)

- **Problem:** at L0, the guns were a kill-chance table using a mean speed. Guns alone killed 0 of 8 sea-skimmers, and no number explained why.
- **Change:**
  - ISA, the G7 curve and firing tables, built once per projectile and cached (C27);
  - ballistic lead on the track's constant-velocity prediction;
  - mounts that slew in the core and fire only once laid;
  - ship roll and pitch × solution latency counted as miss;
  - a shot-group hit, with one seeded draw per burst and its sum in the log (C28).

  L1 without data throws.
- **Lessons:**
  - The form factors were CALIBRATED to published ranges: Mk 45 0.9251, Mk 149 3.2484, Mk 38 1.2057. Phalanx's 3.25 means G7 fits that penetrator poorly (C26).
  - Lethality figures are AUTHORED and stay unverified.
  - L0's golden Fix held at `2AF5C040E4AC6B5E` because L0 does not use the ballistics.
- **Pinned:** `GunsL1Tests` (14 tests today), `Calibration~/GUNS-SOURCES.md`.

## `d02d226`: presentation mounts follow the core's lay (2026-10-24)

- **Problem:** the core trained and elevated the mounts, but the ship model ran its own slew toward each burst's aim point. What you saw was not what fired.
- **Change:**
  - `IGunMount.TryGetLay`;
  - `MountAnimator` follows the lay at twice the rig's rate (smooth between 50 Hz steps);
  - `CombatPresentation` rebinds to each new run's mounts.
- **Lesson:** presentation reads the core's state and never aims on its own. A new run means new mount objects, so rebind.
- **Pinned:** `PresentationTests.MountAnimator_AtGunsL1_ShowsTheCoresLay` (within 0.01°). In play mode, the model stayed within 0.13° of the core over 301 samples.

## `76c2d6a`: the Mk 45 jerked between shots (2026-10-24; AP21)

- **Problem:** SIX, on the range: the Mk 45 jerked back and forth between shots. Logged per tick, it was a decision flicker, not the motion: 48 target drops, 120 target changes and 86 train reversals in one raid.
- **Cause:** shots were flown only to 100 m below the muzzle. At 14–20 km, the elevation just under the solution ended short of the target's range, so `TrySolve` had no lower neighbour and returned "out of reach" on alternate ticks.
- **Change:**
  - fly shots to 3,000 m below the muzzle (`FiringTable.cs:36`), and bracket against the floor when a neighbour is missing (`:183`–`186`);
  - `GunMountL1.TargetTrackId`, so the decision can be logged.
- **Lessons:**
  - Cover the whole query domain with margin, and sweep it densely in a test (every 25 m), not at a few points.
  - When something jitters, log the decision per tick before touching the motion.
  - The fix moved the L1 saturation Fix to `79981241FC806144`, because the Mk 45 now kept firing (2 kills, up from 1). That is a determinism test, not a golden.
- **Pinned:**
  - `GunsL1Tests.FireControl_SolutionPassesThroughTheAimPoint_AndRefusesWhatIsOutOfReach` (the sweep);
  - `Mk45_StaysOnItsTarget_UntilItIsGone` (0 drop-and-regains; reversals no more than target changes + 2);
  - METHODOLOGY AP21; the General Quarters `ROADMAP.md` skip/fail log (`:402`–`407`).

## `118e8829`: the Gunnery module; General Quarters' ballistics move in (2026-10-27; K1–K6)

- **Problem:** arming the jets would have meant a second copy of round flight. SIX: "the gun and ballistics works should be maintained and authored by another module".
- **Change:**
  - `git mv` of `Core/Ballistics` into `Assets/Gunnery`, so the `.meta` GUIDs stayed; the namespace became `DeepWater.Gunnery.Ballistics`;
  - General Quarters' three asmdefs reference `DeepWater.Gunnery`;
  - Gunnery got its own banned-API scan (copied: General Quarters no longer scans the moved files) and a dependency-direction test;
  - Fast Play's static-state scan now covers Gunnery.
- **Lessons:**
  - A move keeps GUIDs only with `git mv` of the file and its `.meta`.
  - Moved files leave their old module's guard behind, so give the new module the guard.
  - Fast Play's allow-list is kept by hand: it had gone stale, and that showed as a failing test.
- **Pinned:**
  - `GunneryCoreBannedApiTests` (3 tests);
  - Fast Play's `StaticStateTests`, which allow-lists the Gunnery tables and `FiringTable.s_cache` (`:36`–`39`, `:62`).

## `3e34e232`: the jets armed; everyone fires (2026-10-27; W55–W57, A12)

- **Change:**
  - Gunnery `Core/Guns`: `GunProfile`, `RoundFlight`, `RoundsInFlight` and `LeadSight`; RH Shared gained `HitReport`.
  - Air Wing:
    - the trigger;
    - rounds, carry and hit points, in the Fix;
    - Killed, then falling, then Crashed;
    - the guns stage, the profile and Apply Gun Data;
    - the HUD pipper, and tracers added by the runner.
  - RH Aviator: trigger discipline by skill, and gun tracking.
- **Lessons:**
  - Fly the rounds already in flight first, then fire this tick's. Then firing order cannot decide who is hit (`FlightCore.cs:146`–`148`).
  - Gun tracking through the height loop, with an integral, porpoised: misses of 4–187 m. Asking the hands for the flight-path angle that puts the bore on the lead point settled it under 1 m (RH Aviator `BLUEPRINT.md:130`).
  - Arming changed the old fights. The position-fight tests now fly guns safe (`TriggerMils` 0), and `GunFightTests` is the armed fight (`BLUEPRINT.md:183`).
- **Measured:** 256/256 tests; in play, Dogfight 4 v 4 for 120 s gave 307 rounds, 11 hits and one kill.
- **Not verified:** a human firing.
- **Pinned:** Gunnery `GunTests` (7), Air Wing `GunTests` (5), RH Aviator `GunFightTests` (3).

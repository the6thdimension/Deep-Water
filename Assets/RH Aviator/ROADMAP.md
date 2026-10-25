# RH Aviator — Routine Roadmap

The routine reads this file each iteration, picks the topmost unchecked item, executes,
verifies, commits on green, reverts on red.

**Instruction:** the routine MAY reorder dependencies or add items. It MUST NOT delete
unchecked items without marking `~~skipped~~` + reason.

---

## Phase 0 — Foundation

- [ ] **Mine legacy flight code for domain knowledge.** Read
  `_Archive/Aircraft_Handlers_legacy/MouseFlight/Scripts/AI_FlightController.cs`,
  `_Archive/FighterJet Scripts_legacy/FlightControllerManager.cs` (both archived 2026-10-25; their
  checklists are in each archive folder's README),
  `JMAC/Agents/*.cs`. Summarize intercept geometry, throttle curves, target-selection
  heuristics into `Assets/RH Aviator/_notes/legacy-mining.md`. **Do not modify the
  legacy files.**
- [ ] **State + profile structs.** `AviatorState` (current decision context: assigned track,
  fuel, energy, role state). `AircraftProfileData` (mass, thrust curve, max-G, fuel,
  weapons loadout summary). Unmanaged. (C4)
- [ ] **AircraftProfileSO + RoleProfileSO.** Authoring assets. `Bake()` to structs. (P1)
- [ ] ~~skipped~~ **IAircraftActuator contract.** Under `Core/Contracts/`. Pitch / roll / yaw / throttle
  commands. Wheel-brakes optional. Gear/flaps optional. *Skipped 2026-10-25:* Air Wing's `ISeat` and
  `PilotInputs` are the contract (Air Wing BLUEPRINT W5, W6). RH Aviator implements AI seats against them.
- [ ] **IDecisionMaker contract.** Pure-C# decision-loop driver. Input: world snapshot
  (own state, sensor report, threats). Output: a `FlightCommand`.
- [ ] **AviatorEntity pure-C# core.** Holds state. Pumps Decide → Actuate. (P6)
- [ ] **AviatorBehaviour MonoBehaviour adapter.** FixedUpdate pump. Binds a concrete
  `IAircraftActuator` (the airframe). (P2)
- [ ] **EditMode test scaffolding.** Assembly definition + first construction smoke test.

## Phase 1 — L0 Waypoint follower

- [ ] **WaypointFollowerDecisionMaker.** Reads a `WaypointPath` SO. Computes pitch/roll
  to fly toward next waypoint. No combat.
- [ ] **NullActuator + StubAircraft for tests.** Lets EditMode tests run AviatorEntity
  without Unity physics.
- [ ] **EditMode tests:** waypoint progression, end-of-path behavior, distance tolerance.

## Phase 2 — L1 Single-target intercept

- [ ] **Sensor input adapter.** Inject `ISensorReport` into AviatorEntity. Pick a
  `ITargetTrack` by faction + priority.
- [ ] **Lead-pursuit intercept geometry.** Compute lead point from track velocity.
  Output flight command.
- [ ] **Bingo / RTB logic.** Watch fuel state, return on `bingoFuelKg`.
- [ ] **EditMode tests:** intercept converges on a constant-velocity target within N sec,
  RTB triggers at the right fuel level.

## Phase 3 — L2 Air-combat maneuvering

- [ ] **Energy state model.** Specific energy (PS) tracked in `AviatorState`. Decisions
  favor maintaining advantage.
- [ ] **Pursuit selector.** Lag / pure / lead based on geometry + energy.
- [ ] **Gun snapshot detection.** When target is in cone + range + closure, fire gun.
  (Gun model lives in a `WeaponSystem` consumer — RH Aviator just signals fire.)

## Phase 4 — L3 BVR + countermeasures

- [ ] **Missile launch logic.** Consume Guided Fury via a producer-side contract — request
  a launch from a `IMissileLauncher` (defined in RH Shared if Guided Fury exposes it).
- [ ] **Notch maneuver.** Beam-on incoming missile, drop chaff.
- [ ] **Defensive RWR adapter.** Take radar-warning data; trigger defensive routine.

## Phase 5 — L4 Multi-ship tactics

- [ ] **Wingman role.** Stay in formation, support lead, take wingman shots.
- [ ] **Element / division.** N-ship coordination over a shared `DoctrineSO`.

## Phase 6 — L5 Mission-level AI

- [ ] **Strike package.** Air-to-ground role with SEAD escort.
- [ ] **Tasking interface.** External tasker assigns roles per aircraft; AI executes.

---

## Skip / fail log

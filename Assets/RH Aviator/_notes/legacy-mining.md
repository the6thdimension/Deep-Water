# Legacy mining (slice 3a, BLUEPRINT A6)

**Read 2026-10-26, read-only; nothing was edited.** The question: does the legacy flight code hold pursuit, throttle or turn heuristics worth carrying into the AI pilot?

| File | What it is | Carried? |
|---|---|---|
| `_Archive/Aircraft_Handlers_legacy/MouseFlight/Scripts/AI_FlightController.cs` (217 lines) | Despite its name, MouseFlight's camera and mouse-aim rig: a mouse-driven aim transform, a damped camera, a "frozen" aim direction while free-looking, a boresight and mouse-aim screen position. No AI, no throttle, no pursuit | **The aim-point idea only**, which Air Wing's `MouseInstructor` already carries (W15, credited to MouseFlight, MIT). Nothing new |
| `_Archive/FighterJet Scripts_legacy/FlightControllerManager.cs` (84 lines) | A status-light board: a `ModuleState` enum (Active, Offline, Warning, …) painted onto indicator renderers, with references to a waypoint manager and a route optimiser it never calls | **Nothing.** The rule log (W36) replaces it with something that records why, not a colour |
| `JMAC/Agents/SimpleHomingMissile.cs` (59 lines) | A turn-rate-limited homing projectile: `RotateTowards` the target by a max angle per frame, then move along the nose | **Nothing:** a weapon (out of scope, BLUEPRINT §6.5). The "turn toward the aim at a capped rate" shape is the hands' bank and G caps already |
| `JMAC/Agents/WeaponSystem.cs`, `SimpleDamageable.cs` | Firing and hit points | **Nothing:** weapons and damage are out of scope |

**Verdict:** no heuristics worth carrying. The hands and the Navigate mode are written new against Air Wing's L1 jet, with the safe limits in mind. This note exists so the next session does not re-read these files hoping for more.

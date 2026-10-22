# Deep Water — Flight Stick Input

VelocityOne (and any HID flight stick) integration. New Unity Input System (1.14.2+).

## Architecture

```
[ VelocityOne.inputactions ]   ← Input Actions asset (abstract action names + bindings)
            │
            ▼
[ FlightStickInput ]   ← single component, reads actions, exposes typed properties + edge events
     │      │      │
     │      │      └────▶ FlightStickHud         ← diagnostic IMGUI overlay
     │      │
     │      └────▶ FlightStickToTestRunner       ← trigger → fire missile
     │
     └────▶ FlightStickToCarrier                  ← throttle → ship speed; rudder → ship rudder
```

The pattern: **one reader, many adapters.** Adapters subscribe to the reader's events or poll its properties — they never wire `InputAction.CallbackContext` themselves. That way a single trigger pull can't accidentally fire in three different places out of order.

## First-time setup

1. **Add the input root to the scene.**
   - Create an empty GameObject named `_Input`.
   - Add `FlightStickInput`.
   - Drag `VelocityOne.inputactions` into its `Input Actions` field.
   - Add `FlightStickHud` to the same GameObject for binding verification.

2. **Bind your physical stick.** Open `VelocityOne.inputactions` in the Project window. The default bindings target generic `<Joystick>/*` paths that **should** auto-detect most flight sticks — but the VelocityOne's specific paths may need a click-by-click rebind for the throttle levers (B9 / B11) and the buttons. Use the Input Actions editor's **"Rebind"** option for each action.

3. **Verify with the HUD.** Press Play. The top-left overlay shows live axis values. Move each axis, push each button, confirm the right logical action lights up. **If "Roll" doesn't move when you tilt the stick, your binding is wrong — fix it before adding adapters.**

4. **Add adapters as needed.**
   - To fire missiles with the trigger: add `FlightStickToTestRunner` to any GameObject in a scene with a `GuidedFury_TestRunner`.
   - To drive the carrier helm: add `FlightStickToCarrier` to any GameObject in a scene with a `CarrierBehaviour`.
   - To fly the F-18 / control the camera: Phase 2 (not yet shipped).

## Adapters

| Adapter | What it does |
|---|---|
| `FlightStickToTestRunner` | Trigger → first `GuidedFury_TestRunner.FireOnce()`. Salvo button → first `LodComparisonRunner.FireSalvoNow()` or `SalvoRunner.FireNow()`. |
| `FlightStickToCarrier` | Throttle lever → `CarrierBehaviour.SetThrottle` (remapped from [-1..+1] to [0..1]). Yaw (or ThrottleSecondary) → `SetRudder`. Deadzone applied. |

Each adapter auto-discovers its target on the same scan cadence (0.5 s) — no per-runner Inspector wiring needed.

## Keyboard fallback

Every action has keyboard bindings as a fallback so you can develop without the stick plugged in:

| Action | Keys |
|---|---|
| Pitch / Roll / Yaw | W S / A D / Q E |
| Throttle | `[` / `]` (decrement / increment) |
| Fire missile | Space |
| Fire salvo | Left Shift |
| Next camera target | Tab |
| Camera toggle | F |
| Pause | P |

## Why the new Input System and not legacy `Input`

The VelocityOne has dual analog throttles, a hat switch, ~10 buttons, and a twist axis. Legacy `Input.GetAxis("Joy 0 Axis 1")` works for two axes; past that it's a tangle of `Joy 0 Axis N` strings with no asset-level binding rebind support and no edge-event API for buttons. The new system gives us an `.inputactions` asset, runtime rebinding, edge events, and a clean separation between physical bindings and logical actions — exactly the abstraction we need so the same trigger can drive missiles, fire salvos, or release a tailhook depending on which adapter is loaded.

## Methodology cross-references

- **P5** (vendor read-only) — adapters never modify vendor scripts. `FlightStickToTestRunner` polls public APIs only; the F-18 adapter (Phase 2) will inherit from AerialArcade's `BaseAirplane_Input` to extend without modifying.
- **Pat1** (SO-Profile + Pure-Core + MB-Adapter) — the action map is the "profile" (data), `FlightStickInput` is a pure(ish) reader, adapters are the MB layer. Same shape as the rest of the project.
- **AP4 avoidance** — adapters never reach into other systems' internals. They use public methods (`FireOnce`, `SetThrottle`, etc.). If a system doesn't have the public API yet, that's a signal to add it, not to side-channel around.

## MissileRange: VelocityOne launcher + Xbox truck

The saved MissileRange scene has separate device bindings: joystick actions operate
only the launcher; XInput actions operate only the truck.

- VelocityOne X axis: turret traverse; Y axis: elevation (pull back raises).
- Trigger: hold 0.25 seconds to launch at the selected target; release for another shot.
- Hat up/down: next/previous target. Hat left/right: previous/next camera mode.
- Xbox left stick X: steering. RT: forward. LT: brake while moving forward, then reverse.
- Xbox A: handbrake. Both triggers together: brake.

The truck uses six WheelColliders and a Rigidbody. The front axle steers and all
six wheels drive. Tuning lives in MissileRangeTruck.asset (speeds in m/s).
TruckDriving.inputactions holds the editable Xbox bindings. The flight reader
uses a runtime copy of VelocityOne.inputactions so re-enabling does not accumulate
callbacks or disable another reader's shared action map.

To reapply scene wiring: Deep Water > Input > Setup Missile Range Controllers.
This command requires MissileRange open in Edit Mode and saves that scene.
The scene builder predates driving; run this setup again after rebuilding the range.

Hardware check: in Play Mode move the stick and verify turret direction, then
hold/release the trigger. If the device firmware reports its physical trigger on
a numbered button instead of the named trigger control, rebind Trigger in
VelocityOne.inputactions to that observed button. Verify RT drives forward, LT
brakes/reverses, the left stick steers, and A stops the truck. Disconnect each
controller and confirm the other continues operating without stuck input.

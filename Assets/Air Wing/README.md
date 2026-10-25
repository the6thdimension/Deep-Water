# Air Wing

Deep Water's aircraft module. It flies the F/A-18E Super Hornet first, with its own flight model and a fidelity ladder per part.
- `BLUEPRINT.md`: what Air Wing is and every decision behind it (W1–W31).
- `ROADMAP.md`: what is done and what is next.
- `Calibration~/F18E-SOURCES.md`: where every number comes from.

## Fly it (the flight-test scene)

1. Open `Examples/Scenes/AirWing_FlightTest.unity`. To rebuild it: **Deep Water → Air Wing → Build Flight Test Scene**.
2. Press Play. The jet starts airborne at 1,500 m (about 4,900 ft) and 400 kt over the Sea State sea, with the throttle at 89 % military power.
3. Fly it with whichever device you touch. The last device you move owns the stick, and the HUD shows which (`SEAT PLAYER · Gamepad`).

| | Xbox pad | Keyboard | Flight stick (VelocityOne or any HID stick) |
|---|---|---|---|
| Pitch | left stick (back pulls) | S pulls, W pushes | stick (back pulls) |
| Roll | left stick | A / D | stick |
| Pedals (no effect at L0) | LB / RB | Q / E | twist |
| Throttle | RT up, LT down | `]` up, `[` down | lever |
| Afterburner | push the throttle past 90 % of the lever | same | lever past 90 % |
| Flaps / speed brake / gear | Y / X / B | F / B / G | buttons 2 / 3 / 4 |
| Mouse aim | — | move the mouse: the instructor flies to where you point; any stick or key takes over at once | — |

**At L0 (today) the jet:**
- **cannot take off or land**: there is no landing gear until L1, so meeting the sea or a runway is a crash;
- ignores the pedals: a point mass flies without sideslip;
- has a top speed that is not right yet (Mach effects come at L2);
- shows its gear down in flight (the model's rest pose; gear animation comes with Landing Gear L1).

**Not yet verified on real hardware.** The bindings are proved with virtual devices (EditMode tests, and one play-mode run with a virtual Xbox pad). If the VelocityOne lever reads backwards, tick `invertLever` in `ScriptableObjects/PlayerSeatSettings.asset`. Deadzone, mouse sensitivity, throttle rate, the afterburner detent and the instructor's gains are in the same asset.

## What is deliberately not here

- Aircraft weapons, sensors and countermeasures: BLUEPRINT §6, constraint 8. Only if SIX asks.
- Carrier operations: parked until the AI stream is done (W16).
- A cockpit view (later), the helicopter (its own stream after jet L1), multiplayer.

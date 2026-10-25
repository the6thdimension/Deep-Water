# Aircraft_Handlers (legacy)

**Archived 2026-10-25** by Air Wing slice 0 (BLUEPRINT W19, METHODOLOGY P8 and C25). No scene or prefab used any of these scripts (GUID scan, 2026-10-24). None of them is a flight model: they are taxi code, tutorial car-driver scripts, and a copy of a third-party mouse-flight kit.

**Two changes on the way in:**
- **Live audio moved out, not archived.** `FixedWing_Handlers/Audio/` (`newF85 Sounds/afterburner.mp3`, `mid55.wav`) and `TRANSPORTATION_JET_MILITARY_Fighter_Turbine_Steady_Soft_04.wav` are used by the F-18E's audio in CVN-78 FORD, Dogfight and OutdoorsScene. They now live in `Assets/_AIRCRAFT/Audio/`, with the same GUIDs.
- **`FixedWing_Handlers/Editors/` was renamed `Editor/`.** "Editors" is not a special folder, so `FixedWingController_Editor.cs` (which uses `UnityEditor`) was compiled into the player and broke every player build.

| File | What it did | Disposition |
|---|---|---|
| `FixedWing_Handlers/FixedWingController.cs` | Taxi-only handling: states (Cold, Starting, Hot, Taxiing, Flying), throttle force, nosewheel steering by rotating a transform, NWH wheel brake torque; legacy `Input` in `Update` | **Superseded** by Air Wing Landing Gear L1: brakes and nosewheel steering through PhysX contacts (ROADMAP, Phase 2) |
| `FixedWing_Handlers/TaxiController.cs` | A simpler taxi: forward force plus nosewheel rotation, in `Update` | **Superseded** by Landing Gear L1 |
| `FixedWing_Handlers/AIFixedWiing.cs` | Taxi-to-a-point AI driving `FixedWingController` (throttle, brake near the target, nosewheel left or right) | **Dropped deliberately:** taxi AI is not in the plan. Revisit with the carrier stream's deck handling |
| `FixedWing_Handlers/Editor/FixedWingController_Editor.cs` | Custom inspector for `FixedWingController` | **Dropped** with it |
| `FixedWing_Handlers/TaxiAI/Mouse3D.cs`, `Scripts/CameraTarget.cs`, `CarAITargetMouse.cs`, `CarDriver.cs`, `CarDriverAI.cs` | Tutorial car-driver scripts: a mouse-picked world target, a camera target moved by keys, a car driver and its follow-the-target AI | **Dropped deliberately:** car code, never used on an aircraft |
| `MouseFlight/Scripts/MouseFlightController.cs` (© Brian Hernandez, MIT) | Camera rig and mouse aim: the aircraft flies toward where the mouse points | **Superseded** by Air Wing's mouse-aim instructor (W15, slice 1c): our own code; the idea is credited to MouseFlight |
| `MouseFlight/Scripts/AI_FlightController.cs` | A copy of `MouseFlightController` whose aim point is a waypoint transform, in `Update` | **Superseded** by RH Aviator AI L0 (Phase 3) |
| `MouseFlight/Scripts/AI_Plane.cs` (© Brian Hernandez, MIT, modified) | The MouseFlight plane with keyboard override; forces in `FixedUpdate`, tuning on the component | **Superseded** by Air Wing's L0 airframe and the instructor (slices 1b, 1c) |
| `MouseFlight/Scripts/AI_Airplane.cs` | Animated the propeller, flaps and rudder from `AI_Plane`, in `Update` | **Superseded** by slice 1d's control-surface animation |
| `MouseFlight/Scripts/Waypoint.cs`, `WaypointTester.cs` | Trigger volumes that advanced `AI_FlightController` or logged "TRIGGERING" | **Superseded** by RH Aviator AI L0 routes |
| `MouseFlight/Demo/Scripts/Plane.cs`, `Hud.cs` (© Brian Hernandez, MIT), `Demo/Prefabs/MouseFlightHud.prefab`, `Demo/UI/*.png` | The MouseFlight demo plane, HUD and crosshairs | **Superseded** by the Air Wing L0 airframe and HUD (slices 1b, 1d) |
| `NWSTow.cs` | Turned the nosewheel toward a tow target while an RCC trailer hitch was towed | **Dropped deliberately:** tow tractors are not in the plan. Revisit with the carrier stream's deck handling |

**Licence:** the MouseFlight files keep their MIT headers. Removing a copyright notice from them would break the licence.

**Still used by:** nothing (the live audio moved out, see above).

**To restore:** move the folder (with its `.meta` files) back to `Assets/Scripts/Aircraft_Handlers/`, and the audio back to `FixedWing_Handlers/Audio/` and the folder root.

**Delete only when** no scene or prefab references these GUIDs.

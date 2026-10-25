# FighterJet Scripts (legacy)

**Archived 2026-10-25** by Air Wing slice 0 (BLUEPRINT W19, METHODOLOGY P8 and C25). No scene or prefab used any of these scripts (GUID scan, 2026-10-24), and none of them flies an aircraft.

| File | What it did | Disposition |
|---|---|---|
| `AirWaypointManager.cs` | A list of air waypoints (position, altitude, speed, expected time) with add, remove and clear | **Superseded** by RH Aviator AI L0 routes (Air Wing ROADMAP, Phase 3). Carried as an idea: a waypoint holds an altitude and a speed as well as a position |
| `FlightControllerManager.cs` | Coloured a status light per "module" from a ten-state enum, in `Update`; flashing was never written. Referenced `WaypointManager` (in `_SCENES/Rocket/`) and `RouteOptimizer` | **Superseded** by the Air Wing HUD, which names each part's level and any fallback (slice 1d). The status-light colours are **dropped deliberately**: nothing set the states |
| `RouteOptimizer.cs` | An empty `OptimizeRoute()`, plus an unused private sort by distance | **Dropped deliberately:** an empty stub |
| `Editor/FlightControllerManagerEditor.cs` | Custom inspector for `FlightControllerManager` | **Dropped** with it |

**Still used by:** nothing.

**To restore:** move the folder (with its `.meta` files) back to `Assets/Scripts/FighterJet Scripts/`.

**Delete only when** no scene or prefab references these GUIDs.

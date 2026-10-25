# Dogfight (legacy)

**Archived 2026-10-25** by Air Wing slice 1e (BLUEPRINT W12, W13, W19; METHODOLOGY P8 and C25). `Assets/_SCENES/Dogfight.unity` is now built by **Deep Water → Air Wing → Build Dogfight**, which overwrites it in place (same GUID, `6981d1a2f44fd5f4e8766d22cb99db41`). The scene as it was before, and the dead jet only it used, are kept here.

| File | What it was | Disposition |
|---|---|---|
| `Dogfight.unity` | A copy (new GUID) of the scene before the rebuild: the FighterJetController `F-18 (1) Variant` at 106 m; two switched-off FDS F-18Es on AerialArcade (with `XboxAirplane_Input` and `AirplaneAudio`); `DDG51 (1) Variant` (its two destroyer scripts are missing since the General Quarters archive); an `RH_RadarModule`; HDRP Ocean water; AerialArcade `FlightWaypoint` and `TestWaypoint` markers; a `Cat 1` empty; three engine-audio sources | **Superseded** by the rebuilt Dogfight: the Air Wing F/A-18E, the Sea State sea, land with runway 09/27, four gates, the DDG-51 model as a landmark. **Dropped deliberately:** the radar module (Radar Suite does not use it here; the script stays for Rocket and SENTINAL), the AerialArcade waypoints and the `Cat 1` marker (nothing flies to them), and the engine audio (Air Wing has no engine sound yet; the clips stay in `_AIRCRAFT/Audio` for CVN-78 FORD and OutdoorsScene) |
| `F-18 (1) Variant.prefab` | The F-18 with `FighterJetController`, from `Assets/_AIRCRAFT/` | **Superseded** by `Assets/Air Wing/Prefabs/FA18E_AirWing.prefab` (slice 1d) |
| `FighterJetController.cs` | 776 lines from `Assets/_SCENES/Rocket/`: thrust, lift and rates by hand on a Rigidbody, plus waypoint patrol and "aggressiveness". Nothing ever called its `HandleFlight`, so the jet never flew (stock-take 2026-10-24) | **Superseded** by the Air Wing flight core: L0 now, L1 6-DOF with fly-by-wire next (Phase 2). Its waypoint patrol is **superseded** by RH Aviator AI L0 routes (Phase 3) |
| `WaypointManager.cs` | 35 lines from `Assets/_SCENES/Rocket/`: click to drop a waypoint and append it to a `FighterJetController`'s list | **Superseded** with it: RH Aviator routes (Phase 3). Archived with the controller because it needs that type to compile |
| `WaypointManager (1).prefab` | From `Assets/_SCENES/`: a `WaypointManager` | **Dropped** with it: no scene or prefab used it (GUID scan, 2026-10-25) |

**Still used by:** nothing but the archived `Dogfight.unity` copy (GUID scan, 2026-10-25; `FR2_Cache.asset` is an untracked cache).

**To restore the old scene:** open `Dogfight.unity` here. To make it the live scene again, copy it over `Assets/_SCENES/Dogfight.unity` (the live file keeps its GUID) and move the two prefabs and two scripts back to `_AIRCRAFT/`, `_SCENES/` and `_SCENES/Rocket/`.

**Delete only when** SIX no longer wants the old scene, and no scene or prefab references these GUIDs.

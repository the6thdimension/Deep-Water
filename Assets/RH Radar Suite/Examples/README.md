# RH Radar Suite Examples

This folder contains example content for the RH Radar Suite.

## RadarKitExample scene (generated)

Run **RH Navy Sims > Radar Suite > Build Example Scene** to generate
`RadarKitExample.unity` here. It contains a ground radar tower, a picket ship
with a naval surface-search radar, an orbiting patrol aircraft with a
body-relative airborne-intercept radar (CircleFlyer), and five targets with
varied RadarSignatures — including a stealth drone, a noise jammer (visible to
the passive LOD), and an orbiting bandit.

### How to Use

1. Build the scene from the menu, then enter Play mode
2. Watch the Scene view: each radar's RadarDiagnostics draws coverage,
   the live sweep, and contacts (measured position, truth ghost, velocity)
3. Toggle the diagnostic layers on the RadarDiagnostics component
4. Use the RadarControlPanel (RH Navy Sims > Radar Suite > Radar Control Panel) to adjust radar settings
5. Switch between different LOD levels to see how detection capabilities change

## One-click radar outfitting

Select any GameObject and use **GameObject > RH Navy Sims > Radar Suite >
Add &lt;role&gt; Radar** — the controller, the profile's default LOD module, and
the diagnostics gizmos are added and configured in one step (idempotent; your
Inspector tuning on existing modules survives). Default profiles are created
under `ScriptableObjects/Profiles/`.

### Key Components

- **Radar platforms**: each with a RadarSuiteController, its profile's LOD module and RadarDiagnostics
- **Target objects**: various objects with different RadarSignature settings
- *There is no in-scene contact panel yet: contacts show in the Scene view gizmos and the Radar Control Panel window.*

## Creating Your Own Radar System

To add radar functionality to your own objects:

1. Add the `RadarSuiteController` component to your object
2. Add the desired LOD module components (PassiveDetectionModule, BasicRadarModule, etc.)
3. Configure the parameters for each module
4. Add `RadarSignature` components to objects you want to be detectable

## LOD Descriptions

- **LOD1 (Passive Detection)**: Detects emissions without transmitting
- **LOD2 (Basic Radar)**: Simple active radar with range detection
- **LOD3 (Doppler Radar)**: Adds velocity detection and moving target identification
- **LOD4 (3D Tracking)**: Full 3D spatial tracking with azimuth and elevation
- **LOD5 (High-Fidelity)**: a stub so far. SAR/ISAR imaging and clutter modelling are not built (ROADMAP Phase 4 replaces this module)

## The pure core (not yet used by the modules)

`Core/Detection` and `Core/Tracking` hold engine-free C# for the radar equation, detection statistics, the radar
horizon, measurement errors, an α-β filter and a track file (ROADMAP Phases 2–3, 2026-10-27). They are tested
(`Tests/Editor`) but the LOD modules above do not call them yet.

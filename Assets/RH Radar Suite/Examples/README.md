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

- **Radar Platform**: Contains the RadarSuiteController and all LOD modules
- **Target Objects**: Various objects with different RadarSignature settings
- **UI Panel**: Displays information about detected contacts

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
- **LOD5 (High-Fidelity)**: Advanced features like SAR/ISAR imaging and detailed clutter modeling

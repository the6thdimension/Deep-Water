# RH Radar Suite Examples

This folder contains example scenes demonstrating the functionality of the RH Radar Suite.

## Basic Radar Example

The `BasicRadarExample` scene demonstrates a simple radar setup with various targets. It includes:

1. A radar platform with the RadarSuiteController attached
2. Multiple target objects with RadarSignature components
3. UI elements to display radar information

### How to Use

1. Open the `BasicRadarExample` scene
2. Enter Play mode
3. Use the RadarControlPanel (RH Navy Sims > Radar Suite > Radar Control Panel) to adjust radar settings
4. Switch between different LOD levels to see how detection capabilities change

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

# Guided Fury - Modular Missile System

## Overview
Guided Fury is a modular missile system for Unity that provides LOD 3 level fidelity with optional higher-fidelity modules. The system is designed with modularity in mind, allowing advanced functionality to be enabled or disabled without breaking the core simulation.

## Architecture

### Core Components (LOD 3)
- `MissileBase.cs`: The foundation class for all missiles
- `MissilePhysics.cs`: Moderate aerodynamic modeling including drag, lift, and flight stability
- `MissileSensor.cs`: Base sensor implementation for target acquisition and tracking
- `MissileGuidance.cs`: Core guidance logic for missile navigation
- `MissileManager.cs`: Global manager for missile spawning, tracking, and pooling

### Advanced Modules (Optional)
- **Sensors**: Advanced multi-band IR, radar modes, ECM/ECCM
- **Guidance**: Advanced guidance algorithms and mid-course updates
- **Flight Control**: High-fidelity aerodynamics and path prediction
- **Networking**: Multiplayer integration and network synchronization

### Guidance Types
- Heat-Seeking / IR (Core)
- Radar-Guided (Core)
- Laser-Guided (Module)
- GPS / INS (Module)

## Usage
1. Attach the `MissileBase` component to your missile prefab
2. Configure the basic missile parameters
3. Add optional advanced modules as needed
4. Use the `MissileManager` to spawn and manage missiles

## Integration
The system uses a modular approach with interfaces and events to allow for easy extension without modifying core code. Each module can register with the core components to enhance functionality while maintaining the ability to run without any advanced modules.

## Advanced Modules

### Sensor Modules
- `AdvancedIRSensor`: Enhances IR detection with multi-band capabilities, improved countermeasure resistance, and weather penetration.

### Guidance Modules
- `TerrainFollowingGuidance`: Enables missiles to fly at low altitudes following terrain contours with obstacle avoidance.

### Flight Control Modules
- `AdvancedAerodynamics`: Provides high-fidelity aerodynamic modeling with realistic forces, moments, and atmospheric effects.

### Configuration
- `MissileConfigSO`: ScriptableObject for storing and applying missile configurations, including advanced module settings.

## Examples
The `Examples` folder contains sample implementations:
- `MissileLauncher.cs`: Basic missile launching and targeting
- `AdvancedMissileExample.cs`: Demonstrates using advanced modules
- `SimpleHeatSource.cs`: Implementation of the `IHeatSource` interface for IR targeting

## Module Implementation
To create a custom module:
1. Implement the `IMissileModule` interface
2. Register with the appropriate missile component in `Initialize()`
3. Add your module logic in `UpdateModule()`
4. Enable/disable functionality through `SetEnabled()`

## Requirements
- Unity 2020.3 or higher
- No external dependencies

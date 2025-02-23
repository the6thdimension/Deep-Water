# Deep Water - Unity Naval Simulation Assets

This directory contains all the assets for the Deep Water naval simulation project. The project appears to be a sophisticated naval combat simulation focusing on realistic missile systems and naval warfare.

## Directory Structure

### Core Systems
- `/Scripts` - Custom C# scripts for game logic and systems
  - Includes missile systems, launchers, and related functionality
- `/Scenes` - Unity scene files
- `/Prefabs` - Reusable game objects and components
- `/Resources` - Runtime-loaded assets

### Combat Systems
- `/AceArcadeMissiles` - Missile system assets and components
- `/GunsAndBullets` - Weapon systems
- `/GunTurrets2` - Turret control systems
- `/rim-162essm` - RIM-162 ESSM (Evolved Sea Sparrow Missile) assets

### Environment
- `/Terrains` - Terrain data and settings
- `/Ocean (1).prefab` - Ocean simulation system
- `/FDS Assets` - Additional environment assets

### Input & Controls
- `InputSystem.inputsettings.asset` - Input system configuration
- `RHMILCOM.inputactions` - Military communications input mappings
- `/StreamDeckIntegration` - Stream Deck controller support

### Visual Effects
- `/VFX Arsenal` - Visual effects library
- `/Gizmos` - Debug visualization tools

### Physics & Simulation
- `/Obi` - Physics simulation system
- `/SensorToolkit` - Sensor and detection systems
- `/RealisticCarControllerV3` - Vehicle physics system

### Third-Party
- `/Plugins` - Third-party plugins and extensions
- `/HurricaneVR` - VR integration system
- `/TirgamesAssets` - Additional asset packages

## Setup & Usage

1. This project is built with Unity (version requirements TBD)
2. Required packages are included in the project
3. Main scenes can be found in the `/Scenes` directory
4. Core gameplay scripts are located in `/Scripts`

## Development

- Use the provided input system settings for consistent control schemes
- Follow the existing component architecture for new features
- Utilize the SensorToolkit for detection and targeting systems
- Leverage the Obi system for physics-based simulations

## Notes

- The project includes VR support through HurricaneVR
- Stream Deck integration is available for additional control options
- Multiple weapon systems are implemented including missiles and gun turrets
- Advanced ocean simulation is included for realistic naval environments

---
For detailed documentation on specific systems, please refer to the respective directories and their documentation.

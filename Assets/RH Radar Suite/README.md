# RH Radar Suite

A comprehensive, modular radar simulation system for Unity, designed for military simulation applications. The RH Radar Suite provides multiple levels of detail (LOD) for radar functionality, from basic passive detection to high-fidelity active tracking with SAR/ISAR-like features.

## Features

- **Modular Design**: Single controller script with modular LOD components
- **Multiple LOD Levels**: Five levels of radar detail from passive detection to high-fidelity tracking
- **Realistic Radar Simulation**: Implements radar equation, signal processing, and detection algorithms
- **Customizable Parameters**: Extensive configuration options for each radar module
- **Visual Debugging**: Built-in visualization tools for radar coverage and contacts
- **Editor Integration**: Custom editor panels for real-time control and debugging
- **Performance Optimized**: Designed for efficient performance in complex simulations

## LOD Levels

1. **LOD1 - Passive Detection**: Detects emissions without transmitting, providing basic directional awareness
2. **LOD2 - Basic Radar**: Actively transmits pulses and measures range to targets
3. **LOD3 - Doppler Radar**: Includes Doppler processing for identifying moving vs. stationary targets
4. **LOD4 - 3D Tracking**: Determines 3D spatial location, range, velocity, and angle measurements
5. **LOD5 - High-Fidelity**: SAR/ISAR-like features for detailed imaging and advanced clutter modeling

## Getting Started

### Basic Setup

1. Add the `RadarSuiteController` component to your object
2. Add the desired LOD module components (PassiveDetectionModule, BasicRadarModule, etc.)
3. Configure the parameters for each module
4. Add `RadarSignature` components to objects you want to be detectable

### Using the Editor

- Access the Radar Control Panel from the menu: RH Navy Sims > Radar Suite > Radar Control Panel
- Use the custom inspector for the RadarSuiteController to configure and debug the radar system
- Enable the RadarVisualizer component to see radar coverage and contacts in the scene view

## Core Components

### RadarSuiteController

The main controller script that manages the radar system and provides access to all LOD functionalities.

### IRadarLODModule

Interface for all radar LOD modules, ensuring a consistent API for each module.

### RadarContact

Represents a detected target with properties for tracking and classification.

### RadarSignature

Defines radar properties of objects, such as size and material type, which affect detectability.

## Advanced Usage

### Events

The radar system provides several events you can subscribe to:

- `OnContactDetected`: Fired when a new contact is detected
- `OnContactLost`: Fired when a contact is lost
- `OnContactUpdated`: Fired when a contact's information is updated
- `OnLODChanged`: Fired when the radar's LOD level changes

### Custom Integration

You can extend the radar system by:

1. Creating custom LOD modules that implement the IRadarLODModule interface
2. Extending the RadarSignature class to add custom properties
3. Implementing custom visualization or UI components that subscribe to radar events

## Performance Considerations

- Higher LOD levels require more computational resources
- Adjust the update interval based on your performance requirements
- Limit the maximum number of targets for better performance
- Use the appropriate LOD level for your specific simulation needs

## License

This asset is part of the RH Navy Sims package and is subject to the same licensing terms.

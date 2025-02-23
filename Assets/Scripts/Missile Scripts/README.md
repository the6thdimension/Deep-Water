# Missile Suite

## Overview
The Missile Suite is a comprehensive system for simulating missile behavior in Unity. It includes scripts for missile configuration, launching, and in-flight behavior, providing a robust framework for missile-based gameplay mechanics.

## Components
- **Missile.cs**: Handles the physics and movement of the missile, including thrust and speed limitations.
- **MissileData.cs**: A `ScriptableObject` that stores configurable missile parameters such as speed, thrust, and guidance type.
- **MissileLauncher.cs**: Responsible for launching missiles and assigning targets.
- **MissileBehavior.cs**: Manages in-flight behavior, including target tracking and impact detection.

## Setup
1. Assign `MissileData` to the `MissileLauncher` in the Unity Inspector.
2. Use the `SetTarget` method to assign a target for the missile.
3. Link a UI button to the `LaunchMissile` method for missile firing.

## Usage
- Customize missile parameters via `MissileData` assets.
- Use the custom editor script for enhanced controls and insights in the Unity Inspector.

## Future Enhancements
- Expand guidance systems for different missile types.
- Integrate more advanced impact effects and damage calculations.

# Lean Build

Player builds leave out shaders that can never render in this HDRP project. Fewer variants means a shorter cold shader compile. Decided by SIX on 2026-10-25; measured in `BUILD-SPEED-LEDGER.md`.

## What it leaves out

| Shader | Why | Cost in the 2026-10-25 cold build |
|---|---|---|
| `Hidden/HDRP/TemporalAA` | Only when no camera in the build uses TAA. Every live camera is set to "No Anti-aliasing" | 1,284 variants, 768 s of compile time |
| `Standard`, `Standard (Specular setup)`, `Autodesk Interactive`, `Particles/Standard Surface`; `Legacy Shaders/*`, `Mobile/*`, `Nature/*` | Built-in render pipeline shaders. HDRP cannot draw them (they render nothing, or magenta); vendor materials pull them in | `Standard` alone: 985 variants |

HDRP's own ray-tracing passes are left out by turning ray tracing off in `Assets/Settings/HDRP High Fidelity.asset` (no volume uses ray tracing). Those passes cost ~290 s of compile time.

## How the TAA guard works

- Unity builds scenes before it compiles shaders, so `LeanShaderStripper.OnProcessScene` checks every `HDAdditionalCameraData` in each scene as it is built.
- If any camera uses `TemporalAntialiasing`, TAA stays, and the build log names the camera.
- If a shader arrives before any scene has been checked, TAA stays.
- **Not covered:** a camera that code switches to TAA at runtime, or one spawned from a prefab no scene contains. If you add either, untick *Strip Temporal AA When Unused*.

## Settings

`Assets/Lean Build/LeanBuildSettings.asset` holds the master switch, the TAA switch and the name and prefix lists. Each build ends with one Console line, and the full list goes to `Library/LeanBuild/last-build.txt`.

## Layout

| Path | Holds |
|---|---|
| `Editor/ShaderPolicy.cs` | The pure decision |
| `Editor/LeanShaderStripper.cs` | Unity's build callbacks |
| `Editor/LeanBuildSettings.cs` | The settings asset type |
| `Tests/Editor/` | Policy tests |

It is editor-only, so there is no runtime part.

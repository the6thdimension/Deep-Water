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

## Background builds

**Deep Water > Build > Build Active Scene In Background** builds the active scene without locking the Editor. It runs `tools/background-build/bg_build.ps1`:

1. It mirrors `Assets`, `Packages` and `ProjectSettings` into `<project> BG`, next to the project and outside the repo. On the first run it also seeds the mirror's `Library` from this one's.
2. It builds the mirror with a headless Unity at below-normal priority.
3. It copies the player and `background-build-report.txt` to `Builds/Background/<stamp>/`.

Save the scene first: the mirror copies what is on disk. The log is in the mirror's `Logs/`.

GQ_Range on 2026-10-25: 276 s the first time, 45 s when nothing had changed (BUILD-SPEED-LEDGER, E2). To build from a shell:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/background-build/bg_build.ps1 -Scenes "Assets/General Quarters/Examples/Scenes/GQ_Range.unity"
```

## Layout

| Path | Holds |
|---|---|
| `Editor/ShaderPolicy.cs` | The pure decision |
| `Editor/LeanShaderStripper.cs` | Unity's build callbacks |
| `Editor/LeanBuildSettings.cs` | The settings asset type |
| `Editor/BackgroundBuild.cs` | The background-build menu item and the headless entry point |
| `Tests/Editor/` | Policy tests |

It is editor-only, so there is no runtime part.

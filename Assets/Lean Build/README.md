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

**Deep Water > Build > Build Active Scene In Background (Release)** or **(Iterate)** builds the active scene without locking the Editor. It runs `tools/background-build/bg_build.ps1`:

1. It mirrors `Assets`, `Packages` and `ProjectSettings` into `<project> BG`, next to the project and outside the repo. On the first run it also seeds the mirror's `Library` from this one's.
2. It builds the mirror with a headless Unity at below-normal priority, using the chosen profile.
3. It copies the player and `background-build-report.txt` to `Builds/Background/<profile>/<scene>/`.

Save the scene first: the mirror copies what is on disk. The log is in the mirror's `Logs/`.

If a Unity has the mirror open (another build, or a session running tests there), the script stops rather than change files under it. Pass `-Mirror <folder>` to use another mirror.

### Profiles

The profiles live in `Assets/Lean Build/Profiles` (`LeanBuildProfile`). They say how the build runs. They are not Unity's Build Profiles, which hold player settings.

| Profile | Development build | Scripts only when only code changed |
|---|---|---|
| `Release` | no | no: always a full build |
| `Iterate` | yes | yes |

**When Iterate builds scripts only:**
- Each profile keeps the files changed since its own last successful build, in `<mirror>/Library/LeanBuild/pending-<profile>.txt`.
- A Release build in between can't hide a texture change from Iterate's next patch.
- Iterate builds only the scripts when every pending change is a `.cs`, `.asmdef` or `.asmref`, or the `.meta` of one, and its last build is there to patch. Anything else builds in full, and the report says why (`BuildPlan`).
- In a scripts-only build, scene and asset data stay as Iterate's last full build left them, as with Unity's own Scripts Only Build. A new serialized field takes its default until the next full build.

To build from a shell:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/background-build/bg_build.ps1 -Scenes "Assets/General Quarters/Examples/Scenes/GQ_Range.unity" -Profile Iterate
```

Measured in BUILD-SPEED-LEDGER, E2 and E5.

## Layout

| Path | Holds |
|---|---|
| `Editor/ShaderPolicy.cs` | The pure decision |
| `Editor/LeanShaderStripper.cs` | Unity's build callbacks |
| `Editor/LeanBuildSettings.cs` | The settings asset type |
| `Editor/BackgroundBuild.cs` | The background-build menu items and the headless entry point |
| `Editor/LeanBuildProfile.cs`, `Profiles/` | The profile type and the Release and Iterate assets |
| `Editor/BuildPlan.cs` | The pure decision: full or scripts-only, and why |
| `Tests/Editor/` | Policy and build-plan tests |

It is editor-only, so there is no runtime part.

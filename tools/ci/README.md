# tools/ci

Verification harness for the autonomous-development routines.

## One-time setup

1. Create `tools/ci/unity-path.txt` containing the absolute path to your `Unity.exe`. Example:
   ```
   C:\Program Files\Unity\Hub\Editor\6000.0.30f1\Editor\Unity.exe
   ```
   (Do not commit this file — `.gitignore` it. The actual path is per-machine.)

2. Verify it runs:
   ```powershell
   pwsh tools/ci/run-checks.ps1 -Module GuidedFury
   ```

## What it does

`run-checks.ps1 -Module <Module>` runs three checks in order:

1. **Compile** — `Unity.exe -batchmode -quit -projectPath . -logFile`. Non-zero exit = compile error.
2. **EditMode tests** — `-runTests -testPlatform EditMode -testFilter <Module.Tests>`.
3. **PlayMode smoke** — runs `Module.Editor.SmokeRunner.Run`, which loads the module's
   smoke scene and pumps it for N seconds. Skipped if no smoke scene yet.

Exits 0 on green, non-zero on the first red step.

## Per-module smoke scenes

| Module | Scene path |
|---|---|
| GuidedFury | `Assets/Guided Fury/Examples/Scenes/MissileRange.unity` |
| RHRadar | `Assets/RH Radar/Examples/RadarSmoke.unity` |
| RHAviator | `Assets/RH Aviator/Examples/AviatorSmoke.unity` |

The greenfield modules' smoke scenes don't exist yet — each routine builds its own
in Phase 0 / Phase 1 work.

---
name: unity-mcp-workflow
description: "Drive the Unity Editor headless via the unity-mcp tools in this project, with ready-made scripts and RunCommand templates. Use whenever running C# in Unity (Unity_RunCommand), editing scripts and waiting for them to compile, running EditMode tests, entering or exiting play mode, recording values over time in play mode (roll periods, positions, anything per fixed step), or capturing screenshots. Use it ESPECIALLY when a compile seems frozen or Unity keeps running old code, when RunCommand says COMPILATION_IN_PROGRESS for minutes, or when a command is refused with 'User interactions are not supported'. Covers the CommandScript contract, the play-mode compile freeze, the junction/file-watcher trap, compile-wait and file-wait scripts, the TestRunnerApi results-file pattern, the play-mode recorder plus period analysis, and why hand-rendered HDRP captures come out black."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Unity MCP Workflow (Deep Water)

Playbook for driving this project's Unity Editor through `mcp__unity-mcp__*`. First distilled from friction on 2026-09-08. The toolkit (scripts and templates) was added 2026-10-23 after a session that made 229 RunCommand calls, 92 compile waits, 36 test runs and 38 play-mode recordings.

## The toolkit

Paths are relative to this skill folder. The scripts find the project root themselves; override with `UNITY_PROJECT_ROOT`.

| Need | Use |
|---|---|
| Wait for an edit to compile, or see why it didn't | `scripts/wait_build.sh mark`, then trigger the compile, then `scripts/wait_build.sh <Assembly> <NewSymbol> [timeout]` |
| Force Unity to pick up edits | `templates/recompile.cs` as the RunCommand body |
| Run EditMode tests | delete the old results file from the shell, then `templates/run-tests.cs`, then `scripts/wait_file.sh <out> 300 '^FINISHED'` |
| Record values per fixed step in play mode | `Unity_ManageEditor Play`, then `templates/play-recorder.cs`, then `scripts/wait_file.sh <csv> 300 '^# end'` |
| Period, decay and peak of a recording | `python -I scripts/periods.py <csv>` |
| Leave play mode cleanly | `templates/exit-play.cs`, then `Unity_ManageEditor GetState` |

The templates hold `__PLACEHOLDERS__`. Paste the file's contents into `Unity_RunCommand` `Code` after filling them in. Templates live outside `Assets/`, so Unity never compiles them.

### The edit → compile → test loop

```
scripts/wait_build.sh mark                       # 1. before anything triggers a compile
# 2. edit the .cs files (Unity's auto-refresh may start compiling on its own; that's fine)
# 3. RunCommand templates/recompile.cs           (skip if RunCommand answers COMPILATION_IN_PROGRESS)
scripts/wait_build.sh DeepWater.Sea.Tests.Editor MyNewTestName   # 4. a symbol that only exists after the edit
rm -f <scratch>/tests.txt                        # 5. from the shell, never from RunCommand
# 6. RunCommand templates/run-tests.cs
scripts/wait_file.sh <scratch>/tests.txt 300 '^FINISHED'
```

`wait_build.sh` requires the DLL to be **newer than the mark and** contain the symbol, so an old symbol can't fake a pass. It prints exactly one of three outcomes:
- **BUILT:** the assembly rebuilt and contains the symbol.
- **ERRORS:** compile errors after the mark, listed below the outcome line.
- **TIMEOUT:** with a hint. Go straight to the traps below.

## Unity_RunCommand: the C# exec tool

Runs C# by compiling a **transient assembly** against the loaded project assemblies, then executing it.

- **The class must be `internal class CommandScript : IRunCommand`.** `public` gives an "inconsistent accessibility" error. Implement `Execute(ExecutionResult result)`.
- **Logging and object changes:** log with `result.Log(...)`, `result.LogWarning`, `result.LogError`. Register created objects with `result.RegisterObjectCreation`, modifications with `result.RegisterObjectModification` (before mutating), deletions with `result.DestroyObject`.
- **Helper classes go nested inside `CommandScript`, or top-level as `internal`.** A second access-modified class at namespace scope errors as CS1527.
- **Avoid reflection, especially non-public reflection.** It throws bare NREs. Use direct type references, or put such code in a compiled Editor tool.
- **Static state survives across `EditorApplication.update` callbacks** registered by the command. That is how the play recorder works. **It does not survive a domain reload**, and entering play reloads the domain. So send recorders *after* Play.

**Compile-clean check (reliable):** reference every type you care about at the top of a RunCommand. If it compiles and runs, that type's assembly is clean. This is more trustworthy than the console.

## Traps (each cost real time)

1. **Play mode freezes compilation** (2026-10-23, 10+ minutes lost).
   - Symptom: a `.cs` edit made while the Editor is in play mode, even play you didn't start, leaves `EditorApplication.isCompiling` true and every RunCommand answers `COMPILATION_IN_PROGRESS`. `wait_build.sh` times out.
   - Fix: run `Unity_ManageEditor GetState`. If `IsPlaying` is true, `Unity_ManageEditor Stop`, and the compile runs within seconds.
   - Prevention: after any play session, confirm `IsPlaying: false` **before** editing scripts. `ExitPlaymode()` is deferred, so check rather than assume.
2. **`System.IO.File.Delete` inside RunCommand is refused** with "User interactions are not supported for MCP tool calls". Delete files from the shell. Writing files from RunCommand is fine.
3. **The Test Runner blocks on a dirty scene.** If the active scene has unsaved changes, it pops a "save scene?" dialog and waits. `run-tests.cs` refuses to start in that case. Never save a scene you didn't mean to change; ask SIX.
4. **Reimporting a test file in the same command that starts the run** triggers a domain reload that discards your callback. Compile first, run second.
5. **The junction / file-watcher trap** (cost ~30 min once).
   - The project root is a junction: `C:\Users\SIX\Unity Projects` → `F:`. You edit a `.cs`, `AssetDatabase.Refresh()` reports success, and Unity keeps running the old code.
   - Usually there is a genuine compile error in the edited assembly, and Unity silently keeps the last-good DLL. `Unity_ReadConsole` can miss the CS error entirely.
   - Diagnose: `wait_build.sh` reports `ERRORS` from Editor.log directly. If it times out instead, ask SIX to read the Editor's own Console. Then force the rebuild with `templates/recompile.cs` and watch `isCompiling` flip true.
   - **Don't trust `AssetDatabase.Refresh` returning success as proof of recompile.**
6. **The Editor throttles an unfocused player loop**, so game time barely advances. The recorder sets `Application.runInBackground = true`. Project rule: `templates/exit-play.cs` restores `PlayerSettings.runInBackground = false` afterwards.
7. **Hand-rendered `Camera.Render()` to a RenderTexture comes out black under HDRP** (no exposure or volume). Use `ScreenCapture.CaptureScreenshot` from an in-play update hook, which the recorder template supports.
   - Moving the camera in the same frame as the capture adds motion blur. Move it a frame earlier if the image matters.
8. **Unity recomputes its automatic inertia tensor about a scripted centre of mass** (parallel-axis shift, seen 2026-10-23: 2187 → 3794 kg·m² for a 0.7 m drop). Read `rb.inertiaTensor` in play before predicting any rotational period. The recorder logs it.

9. **`wait_build.sh`'s symbol must be code, not a comment** (2026-10-24, 4 minutes lost). Comments never reach the DLL, so waiting for a phrase from a doc comment always times out, even after a good build. Wait for a new class, method or field name.
10. **A RunCommand can do its work and lose its reply** (2026-10-24, 30 minutes). A command that saved assets and a scene, sent just after a forced recursive reimport of a whole module folder, timed out after 1,800 s. The bridge had stopped for a domain reload. The command had in fact run: the assets and the scene were saved. Before re-sending, check the state it should have left (`GetState`, then a read-only RunCommand). Reimport only the folders you edited, never a module root with its FBX and textures.
11. **Someone may be driving the Game view.** SIX plays the range while sessions work. A play recording that shows orders you never gave (a Captain rudder order from the mouse, breach-preset clicks, bells) was a human at the controls. Read the core's order log (`Combat.Orders.All`, with issuers) before you trust a recording, and ask before taking over play mode.
12. **A RunCommand that blocks the main thread gets re-sent, in a loop** (2026-10-25, 30 minutes, ≈75 player builds).
   - What happened: an inline `BuildPipeline.BuildPlayer` held the main thread for 77 s. The relay logged the client as disconnected, then re-sent its cached request when the bridge came back. Each copy built again, blocked again and was re-sent again. Each build took 22–24 s, so the loop ran until the 1,800 s tool timeout and kept going after it.
   - Stop it: make the next copy fail its own guard. Entering play mode (`Unity_ManageEditor Play`) did this, because the command refused when `isPlaying`. Then wait a minute for silence before `Stop`.
   - Prevention: make every RunCommand idempotent: check the state it would leave, and do nothing if it is already there. Never run anything long inline (player builds, full reimports, long bakes). Prefer a menu item you can check by its output file.
   - `EditorApplication.delayCall` is no escape: while the Editor was unfocused it fired 262 s late.

## Play mode & timing

- **Controls:** `Unity_ManageEditor` Actions `Play`, `Stop` and `GetState` (`IsPlaying`, `IsPaused`, `IsCompiling`, `IsUpdating`).
- **Sample per fixed step,** not per Editor update: compare `Time.fixedTime` with the last sample, as the recorder does. Editor updates run faster than fixed steps.
- **Allow for scene transitions** in your timing, such as a sea-state cross-fade (the Sea State library's `transitionSeconds`, 8 s). Wait for the transition plus settling before perturbing.
- **Wait from the shell** with `scripts/wait_file.sh`, never with fixed `sleep`s.

## EditMode tests

- **Run them through `templates/run-tests.cs`:** `TestRunnerApi` with an `ICallbacks` that writes a results file the shell waits on.
- **Assembly names** are the asmdef `name` field, e.g. `DeepWater.Sea.Tests.Editor`, `GuidedFury.Tests.Editor`, `DeepWater.Reckoning.Spike.Tests`.
- **Measurements:** have tests `Debug.Log` tagged lines (`[Buoyancy] …`, `[SPIKE] …`) and pull them from Editor.log:
  `grep -a "\[SPIKE\]" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail`

## Scratchpad

Write all temporary logs, recordings and screenshots to the session scratchpad, not the project. The scripts keep their own marker in the project's `Temp/` folder, which is git-ignored and Unity-owned.

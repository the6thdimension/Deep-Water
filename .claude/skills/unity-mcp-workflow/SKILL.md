---
name: unity-mcp-workflow
description: "Drive the Unity Editor headless via the unity-mcp tools in this project. Use whenever running C# in Unity (Unity_RunCommand), compiling/verifying scripts, entering/exiting play mode, running EditMode tests, or capturing screenshots — and ESPECIALLY when a Unity assembly refuses to pick up source edits or a compile seems frozen. Covers the CommandScript contract, the junction/file-watcher compile trap, reliable recompile + compile-error detection, TestRunnerApi runs, and why hand-rendered HDRP camera captures come out black."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Unity MCP Workflow (Deep Water)

Practical playbook for driving this project's Unity Editor through `mcp__unity-mcp__*`. Distilled from real friction on 2026-09-08.

## Unity_RunCommand — the C# exec tool

Runs C# by compiling a **transient assembly** against the loaded project assemblies, then executing it.

- The class **MUST** be named `CommandScript` and be `internal` (not `public` — "inconsistent accessibility" error). Implement `IRunCommand.Execute(ExecutionResult result)`.
- Log with `result.Log("{0}", x)`, `result.LogWarning`, `result.LogError`. Register created objects with `result.RegisterObjectCreation`, modifications with `result.RegisterObjectModification` (BEFORE mutating), deletions with `result.DestroyObject`.
- **No nested/extra top-level classes with access modifiers** inside the same file — a second `private class Foo` at namespace scope errors as CS1527. Put helper classes at the top level as `internal`, or nest them inside `CommandScript`.
- **Avoid System.Reflection gymnastics** — several reflection-heavy commands threw NREs. Prefer direct type references.

**Compile-clean check (reliable):** reference every type you care about at the top of a RunCommand. If it compiles and runs, that type's assembly is clean. This is more trustworthy than the console.

## The junction / file-watcher compile trap (cost ~30 min once)

This project root is a **junction**: `C:\Users\SIX\Unity Projects` → `F:`. Symptom that WILL recur: you edit a `.cs`, call `AssetDatabase.Refresh()` (reports success), but Unity keeps running the OLD code. A **genuine compile error in the edited assembly** makes Unity silently keep the last-good DLL loaded, and the MCP `Unity_ReadConsole` filter can miss the CS error entirely.

Diagnose in this order:
1. **Ask the user to read the Editor's own Console** (Console window, error rows). They can see CS errors the MCP console misses. This is the fastest path — do it early, don't spend 20 minutes on the file watcher.
2. Confirm whether the assembly actually rebuilt: compare mtimes and grep the DLL for a new symbol.
   ```bash
   DLL="C:/Users/SIX/Unity Projects/RH Navy Sims/Deep Water/Library/ScriptAssemblies/GuidedFury.dll"
   stat -c '%y' "$DLL"                    # vs your source edit time
   python -c "print(b'YourNewSymbol' in open(r'$DLL','rb').read())"
   ```
   If the DLL is older than your edit AND missing the symbol, it did NOT rebuild — there is almost certainly a compile error in that assembly.
3. Force a rebuild that survives the unfocused-editor defer:
   ```csharp
   AssetDatabase.ImportAsset("Assets/.../Changed.cs", ImportAssetOptions.ForceUpdate);
   AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
   UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
   ```
   After this, `EditorApplication.isCompiling` should flip **true**. Then confirm the symbol lands (enum member visible via `System.Enum.GetNames`, or the DLL grep above).

**Do NOT trust `AssetDatabase.Refresh` returning success as proof of recompile.** The tell is `isCompiling` flipping true, followed by the new symbol actually appearing.

## Play mode & timing

- `Unity_ManageEditor` Action `Play` / `Stop` / `GetState` (`IsPlaying`, `IsPaused`, `IsCompiling`, `IsUpdating`).
- The editor **throttles the player loop when its window is unfocused**, so game time barely advances. Set `Application.runInBackground = true` in your first in-play RunCommand so a fired missile/flight actually progresses.
- To sample over time from an editor command, hook `EditorApplication.update += Sample;` and write results to a file under the scratchpad, then poll the file from Bash with an `until grep -q ... ; do sleep 2; done` loop (do not chain fixed `sleep`s).

## EditMode tests

Run via `TestRunnerApi` from a RunCommand. Register an `ICallbacks` writer (top-level `internal` class) that streams results to a scratchpad file; filter `TestMode.EditMode`, `assemblyNames = new[] { "GuidedFury.Tests.Editor" }`. **Reimporting a test file in the same command that starts the run triggers a domain reload that discards your callback** — reimport first, wait for compile, then run in a separate command.

## Screenshots

- In-game: `ScreenCapture.CaptureScreenshot(path)` from an in-play `EditorApplication.update` hook. These are correctly exposed.
- **Hand-rendered `Camera.Render()` to a RenderTexture comes out BLACK under HDRP** (no exposure/volume). Don't use it for overhead/orthographic map shots — use a play-mode camera, or set up a proper HDRP camera + exposure override.

## Scratchpad

Write all temp logs/scripts/screenshots to the session scratchpad dir, not the project. Poll log files with Bash `until`-loops rather than fixed sleeps.

# Full Speed

Keeps the Unity Editor and every process it starts out of Windows 11 power throttling (EcoQoS).

## Why

On a hybrid CPU (Intel P-cores plus E-cores), Windows runs a background app and the processes it starts on the efficiency cores. Unity is a background app whenever another window has focus: your IDE while you save, a browser during a build, or an MCP-driven Claude session.

Measured on the i9-12900H (6 P-cores, 8 E-cores) on 2026-10-25, in `BUILD-SPEED-LEDGER.md`:

| | Throttled | Full speed |
|---|---|---|
| Domain reload | 16.3 s | 7.4 s |
| Shader compiler workers | 5.6 cores busy | 13 cores |
| All EditMode tests | 244 s | 128 s |

The tell: logical processors 12–19 (the E-cores) are pinned at 100 % while 0–11 sit idle. See METHODOLOGY AP27.

## What it does

- On load it sets `Unity.exe`, and every process it starts, to full speed. This is each process's own power-throttling switch, the one Task Manager's "Efficiency mode" flips.
  - That covers `UnityShaderCompiler`, `bee_backend`, `dotnet`/`csc`, the IL post-processing runner and Burst's compiler.
- A background timer rescans the process tree every second, so children started during a player build are caught while Unity's main thread is blocked.
- It changes no Windows setting. Each process's switch ends when that process exits.

## Controls

**Preferences > Deep Water > Full Speed:**
- **Enabled:** on by default.
- **Also on battery:** off by default. A laptop on battery keeps Windows' battery saving.
- **Rescan every (s):** 1 s by default.

Turning it off hands every process back to Windows.

**Deep Water > Full Speed > Report** prints the process tree and each process's state to the Console.

## What is deliberately not here

- No system-wide change. A permanent per-executable exemption is a Windows setting SIX can make himself, as admin: `powercfg /powerthrottling disable /path "<exe>"`.
- No priority or affinity changes: Windows still schedules; it just stops holding Unity on the E-cores.
- Windows only. On other platforms the code compiles to nothing.

## Layout

There is no runtime part, so the standard `Core/`, `Modules/` and `ScriptableObjects/` folders would be empty.

| Path | Holds |
|---|---|
| `Editor/ProcessTree.cs` | Pure tree logic, reachable by tests |
| `Editor/WindowsProcesses.cs` | Win32 snapshot and power-throttling calls |
| `Editor/FullSpeed.cs` | The scanner, the preferences page and the menu |
| `Tests/Editor/` | Tree tests, plus round trips against Windows on a real child process |

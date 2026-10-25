# Build-speed ledger

**One job:** this file records the measured rounds of the build-speed work on `perf/build-speed`. That is the baseline, then each change kept or dropped, with its A/B numbers and the seat that drove the cut. Round 0 is the baseline. A change is kept only if its number moves and behaviour stays put: every EditMode test assembly stays green, and play mode still works (methodology C11).

## The instrument

- **Machine:** 20 logical cores, 40 GB RAM, Samsung 980 PRO NVMe (`F:`, 65 GB free), Windows 11. Unity 6000.4.11f1, HDRP 17.4, Mono scripting backend, D3D11.
  - Unity opens the project through the junction `C:\Users\SIX\Unity Projects` → `F:`.
- **Editor state for Round 0:**
  - Up 5–6 h, with `GQ_Range` open and the Editor unfocused.
  - Other apps were running: OBS, NZXT CAM and Stream Deck, at about 30 % total CPU.
  - So the numbers are noisy. Each figure gives n, the median and the range. A/B runs are interleaved.
- **Where the times come from:**
  - Unity's own Editor.log figures: `Domain Reload Profiling`, `Tundra build success` and `Asset Pipeline Refresh`.
  - Wall-clock times come from a timestamping tail of Editor.log: `.claude/skills/unity-perf-ab/scripts/editor_log_clock.py`.
  - Per-assembly compiler and IL post-processing times come from `Library/Bee/backend*.traceevents`, read by `build_cycle.py`.
  - Attribution comes from one Editor profiler capture of a reload.
- **An edit cycle:**
  - A RunCommand logs `MARK <label>`, writes one line into a file and calls `AssetDatabase.Refresh()`.
  - "Ready" is the log line `Mono: successfully reloaded assembly`.
  - Probe lines were `private const int …` (body-only) or `public const int …` (changes the assembly's API), each removed afterwards.
- **Player builds:** `BuildPipeline.BuildPlayer` for StandaloneWindows64, timed by the BuildReport total, written to the git-ignored `Builds/BuildSpeed/`.

## Round 0: baseline (2026-10-25, `36d0f457`)

*Seat: Brendan Gregg (The Optimizer): measure before you change anything.*

| Measure | n | Median | Range | What's inside |
|---|---|---|---|---|
| Domain reload | 15 | **46.5 s** | 39.0–56.8 s | Earlier today, from the log: 37.4–38.2 s (3) |
| … of which the Unity Version Control window's `OnEnable` | 15 (+1 profiled) | **32.6 s** | 31.2–32.9 s | The phase `AwakeInstancesAfterBackupRestoration`, constant whichever scene is open |
| … an intermittent stall | 5 of 15 | +10.3–10.8 s | | Four times inside `BeginReloadAssembly` (12.6–12.8 s against the usual 1.3–2.6 s), once before the play-mode reload started. **Unattributed** |
| One private line in `DeepWater.Sea` → Editor ready | 4 | **52 s** | 47.9–64.9 s | Refresh 1.7 s, compile, reload |
| … its compile (Tundra) | 6 | **4.7 s** | 3.6–8.6 s | 22–23 nodes: `csc` on Sea (1–3 s), then IL post-processing re-runs on ~20 dependents |
| One public line in `DeepWater.Sea`: compile | 2 | **13.5 s** | 13.3–13.7 s | 67 nodes. `Assembly-CSharp` `csc` 3.6–3.8 s and `-firstpass` 3.2–3.9 s are on the critical path |
| … → Editor ready | 2 | **65.5 s** | 65.4–65.6 s | |
| One private line in legacy `Assets/Scripts/CamRot.cs` (in `Assembly-CSharp`): compile | 2 | **6.55 s** | 6.55 s | `csc` 4.05–4.23 s over all 763 files |
| … → Editor ready | 2 | **67 s** | 66.8–67.3 s | Both caught 56 s reloads |
| Refresh, nothing changed | 6 | 15 ms | 10–18 ms | Not a cost |
| Refresh that finds a changed script, before compiling | 6 | 1.7 s | 1.6–1.9 s | |
| Enter play mode (`GQ_Range`) | 2 | **51 s** | 50.4–52.0 s | Domain reload 39–46 s, then scene load 1.2–1.7 s |
| Exit play mode | 2 | 0.7 s | 0.6–0.8 s | |
| Player build, nothing changed | 3 | **14 s** | 9.6–24 s | Incremental: shader cache and player data cache |
| Player build after a one-line code change | 1 | **77 s** | | The player build program took 38 s. Burst AOT took 65.8 s in a 13:40 build |
| Player build, cold shader cache | 1 (log) | **16.5 min** | | Air Wing benchmark scene, earlier today. Shader passes sum to 805 s of the ~990 s |
| Player-script compile errors | 3 builds | **0** | | The eight blocking files were fixed in `0d24231` (PR #15) |

### Diagnosis

**Domain reload.** A profiled reload took 46.2 s, slower than usual because the profiler was on.

- **`UVCSWindow.OnEnable`: 31.2 s, 30.7 s of it self time.**
  - The Unity Version Control (Plastic SCM) window is docked in the layout.
  - Its plugin sees the tracked `.plastic/` workspace (from 2023, last written 2026-09-08) and calls `REALMHAUS@cloud`.
  - The server answers `InvalidCredentials` and closes the socket. The plugin then retries with 1, 2, 5 and 10 s backoffs on the main thread.
  - It keeps retrying in the background all day, and watches the whole project folder for file changes.
  - Its package is `com.unity.collab-proxy`. Git is this project's version control.
- **`ProcessInitializeOnLoadAttributes`: 4.3 s.**
  - AI Assistant `RunCommandUtils` static constructor 1.9 s (the MCP bridge itself).
  - Entities `BindingRegistry` 0.55 s, Plastic entry point 0.37 s, Input System 0.21 s, UVCS toolbar 0.20 s, App UI 0.14 s.
- **The rest:**
  - Restoring backups 1.7 s: `CesiumEditorWindow` constructor 0.29 s, UVCS 0.13 s.
  - Domain unload 1.6 s, `BeforeProcessingInitializeOnLoad` 1.4 s, `[InitializeOnLoadMethod]` 0.97 s, `BuildScriptInfoCaches` 0.96 s.
  - The Editor loads 215 assemblies.
- **After the reload:** FR2 (Find Reference 2) spends 1.4 s on the first tick (cache init plus check).

**Compile.**

- **Every first-party asmdef has `autoReferenced: true`,** so the four predefined assemblies (`Assembly-CSharp`, `-firstpass`, `-Editor`, `-Editor-firstpass`) depend on all of them.
  - A private edit in `DeepWater.Sea` re-runs IL post-processing on about 20 assemblies, because post-processing reads its references' full DLLs.
  - A public edit recompiles all four predefined assemblies.
  - None of their code uses `DeepWater.Sea`.
- **Three IL post-processors run on every assembly:** Entities codegen, Jobs codegen and Burst.
- **`Assembly-CSharp`: 763 files, 97 k lines.** Obi 370, HurricaneVR 257, first-party legacy about 90 (`Scripts`, `_Archive`, `AerialArcade`, `JMAC`, `RH Testing Suite`).
- **`Assembly-CSharp-firstpass`: 403 files, 79 k lines** (RootMotion 280, RCC 80, HexabodyVR 26, NWH 10, FlightSimLite 7).
- **Editor scripts compile in Debug mode** (the Bee graph is `1900b0aEDbg`). That does not change compile time.

**Player builds.**

- **Cold shader compile:** 3,985 variants, 2,579 CPU-s, all D3D11 through FXC. Parallelism averaged 3.2× on 20 cores.
  - `HDRP/Lit`: 849 CPU-s for 88 variants.
  - `Hidden/HDRP/TemporalAA`: 775 CPU-s, 1,276 variants.
  - `HDRP/Water` 225, `CloudLayer` 131, `FinalPass` 124, Cesium tileset shaders 185 (108 variants).
  - The built-in `Standard` shaders: 96 CPU-s and 985 variants. HDRP cannot render them.
  - Four HDRP assets (the default plus three quality levels) differ in features, and HDRP compiles the union.
- **A code change re-runs Burst AOT:** Obi's Burst backend (24 files), the Reckoning spike, Sea State and the package jobs.
- **Warm builds** reuse `Library/ShaderCache` and `Library/PlayerDataCache`.

**Disk.**

- `Library` is 13 GB. `Artifacts` 4.9 GB, `PackageCache` 4.9 GB (Cesium 2.0 GB, Burst 0.85 GB, AI Assistant 0.55 GB), `BurstCache` 0.9 GB, `Bee` 0.74 GB.
- Both drives are NVMe. `C:` has 32 GB free (3 %).

### Incidents while measuring (kept on the record)

- **The bridge re-sent an inline `BuildPlayer` command about 75 times over 30 minutes.**
  - Each build blocked the main thread, and the relay re-sent its cached request on reconnect.
  - Stopped by entering play mode, so the next copy refused its own guard.
  - Every build succeeded, into git-ignored `Builds/BuildSpeed/GQ`.
  - The rule is now trap 12 in the `unity-mcp-workflow` skill.
- **`EditorApplication.delayCall` fired 262 s late** while the Editor was unfocused.
- **Auto-refresh is erratic while unfocused:** one edit was picked up in 7 s, the next in 237 s. Cycles therefore call `Refresh` explicitly.

## Menu (ranked by time saved in the daily loop): awaiting SIX's picks

| # | Change | Expected gain | Risk | Touches | Whose call |
|---|---|---|---|---|---|
| 1 | Remove `com.unity.collab-proxy` (Unity Version Control); untrack `.plastic/` | Reload 46.5 → ~14 s (−32.6 s, profiled). Edit → ready ~52 → ~20 s; play entry ~51 → ~19 s. Maybe also the +10 s stall (unverified) | Low: git is the VCS, and the plugin cannot log in anyway | `Packages/manifest.json`, `packages-lock.json`, `.gitignore`, `git rm --cached .plastic` | SIX (package removal) |
| 1b | …or only close the UVCS window | The same 32.6 s, on this machine only | None; its background retries and file watcher stay | Editor layout | SIX |
| 2 | Enter play mode without a domain reload (the scene still reloads) | Play entry ~19 s (after #1) → ~2–3 s | Medium: stale static state is a determinism bug. About 23 first-party static fields and collections need resets; vendor statics are unaudited | `EditorSettings.asset`, ~10 first-party files, a two-plays-same-Fix test | SIX |
| 3 | `autoReferenced: false` on first-party asmdefs that the predefined assemblies don't use | Public edit compile 13.5 → ~4 s; private edit ~4.7 → ~3 s | Low: the compiler proves it | ~25 first-party `.asmdef` files | SIX (asmdef work) |
| 4 | Remove unused packages, one A/B each: Visual Scripting, ML-Agents, Cesium (only `Mapping.unity` uses it), PostProcessing v2 (NWH's `PostProcessingController` needs it) | Unmeasured. Fewer of the 215 assemblies to load and scan; Cesium also saves its 0.29 s window constructor, 108 cold variants and 2 GB of cache. Estimate 1–5 s per reload | Low to medium: a feature is lost | Manifest, lock file; PostProcessing v2 also touches a vendor file | SIX (package removal) |
| 5 | A logged HDRP shader stripper for shaders that can never render here (`Standard`, legacy PostProcessing, Cesium if not shipped) | Cold build: ~5–10 % of the shader CPU. The big two (Lit, TemporalAA) need HDRP feature decisions | Low, with a "stripped" log and a build smoke test | New editor script | SIX |
| 6 | Unity Accelerator (free local cache service) | A cold Library or a second machine reuses imports (and shader variants, unverified on 6000.4) | Low; installs a service | Preferences, `EditorSettings` cache-server fields | SIX |
| 7 | Asmdefs inside Obi and HurricaneVR (a vendor change) | Legacy edit compile 6.55 → ~2.5 s | Medium: vendor folders, editor/runtime splits | Vendor folders | SIX |
| — | Kept as they are | Mono is already the Windows backend (IL2CPP would add minutes); Editor Debug mode doesn't affect compile time; the disk is NVMe | | | |

## Rounds

*Each round records the change, its A/B numbers, the tests run, the decision and the seat. SIX picked every item on the menu on 2026-10-25.*

**Behaviour pin (before any change):** EditMode, all seven first-party test assemblies: **301 pass, 3 fail, of 304**. The 3 failures were there before this work:
- `ReckoningSpikeTests.Criterion2_RollPeriod_WithinTenPercentOfImo(A)`: variant A fails by design (R35).
- `GuidedFury.Tests.L3IntegratorTests.L3_AoaProducesLift_PitchedBodyPullsToward`
- `GuidedFury.Tests.SeekerTests.EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses`

Every round must leave exactly this result.

### Round 1: remove Unity Version Control; untrack `.plastic/` (kept)

*Seat: Brendan Gregg (The Optimizer): one profile named the cost, so cut the cost, not the symptoms.*

- **Change:**
  - `com.unity.collab-proxy` removed from `Packages/manifest.json`; the lock file re-resolved.
  - `.plastic/` (5 files, a 2023 Plastic workspace) untracked and added to `.gitignore`.
  - The local folder renamed to `.plastic.pre-git-only-backup-20261025-1651`, not deleted.
- **A/B, domain reload (`RequestScriptReload`, same Editor session, GQ_Range open):**

| | Runs | Reload | `AwakeInstancesAfterBackupRestoration` | `BeginReloadAssembly` |
|---|---|---|---|---|
| A (just before) | 46.6, 56.7 s | median 51.6 s | 32.9–33.0 s | 2.6 s, 12.6 s (the stall) |
| B (after) | 15.1, 15.2, 15.5 s | **15.2 s** | 1.64–1.71 s | 2.5–2.8 s |

- **Result:** reload ×3.0 (46.5 s baseline median → 15.2 s).
  - The 10 s stall (5 of 15 reloads before) did not appear in the 4 reloads since, including the resolve reload at 19.2 s. It was very likely the plugin's retry thread too. That is likely, not proven.
- **Behaviour:** the change is Editor tooling only. It is covered by the test pin after Round 2.
- **Restore:** add `"com.unity.collab-proxy": "2.12.4"` back to the manifest, and rename the backup folder to `.plastic`.

### Round 2: first-party asmdefs off auto-reference (kept)

*Seat: Sam Newman (The Decoupler): an edge nothing uses is still an edge every build walks.*

- **Change:** `autoReferenced: false` in 20 first-party asmdefs:
  - Air Wing ×2, Carrier Ops ×2, General Quarters ×3, Guided Fury ×2, RH Aviator, RH Radar, RH Radar Suite ×2, RH Shared, DeepWater.Input ×2, Sea State ×4.
  - The test asmdefs and the Reckoning spike already had it.
- **What it proves:** the predefined assemblies (`Assembly-CSharp*`) compiled with 0 errors without them, so nothing in them used first-party code. `Unity_RunCommand` still compiles against first-party types (checked with `typeof(DeepWater.Sea.Core.SeaStateDirector)`).
- **A/B, the same one-line probe in `SeaStateDirector.cs`, explicit refresh, 2 runs each:**

| Edit | A compile | B compile | A → ready | B → ready |
|---|---|---|---|---|
| Private line | 3.66, 4.33 s (22 nodes) | **1.56, 1.19 s** (18 nodes) | 24.5, 24.3 s | 24.7, 24.8 s |
| Public line | 12.67, 13.57 s (67 nodes) | **3.40, 3.12 s** (51 nodes) | 34.0, 35.4 s | **25.6, 25.8 s** |

- **Result:** compile ×2.8 for a private edit and ×4.0 for a public one.
  - The private edit's "ready" did not move. In this harness the reload never starts sooner than ~7.8 s after the refresh call, whatever the compile time (see Instrument note below). A faster compile lands inside that floor.
- **Behaviour (R1 + R2):** 301 pass, 3 fail, the same three as the pin.
- **Instrument note:** with the Editor unfocused and driven through the MCP bridge, "Reloading assemblies after finishing script compilation" came at 7.5–8.0 s after the `MARK` in every cycle whose compile ended before that (4 of 4).
